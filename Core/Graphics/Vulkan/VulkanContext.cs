using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Core.Tools.Extensions;
using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public class VulkanContext
{
    private readonly Vk _vk;

    public VulkanContext()
    {
        _vk = Vk.GetApi();
    }

    public VulkanInstance CreateInstance(ApplicationInfo info)
    {
        unsafe
        {
            var vkAppInfo = info.ToVkAppInfo();

            var extensions = Glfw.GetApi().GetRequiredInstanceExtensions(out var countExt);

            var vkInstanceInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = vkAppInfo,
                EnabledExtensionCount = countExt, PpEnabledExtensionNames = extensions
            };
#if DEBUG
            if (!CheckValidationLayersSupport(Constants.DefaultValidationLayers))
                throw new NotSupportedException("Vulkan validation layers requested, but not available.");

            var enabledLayersPtr =
                Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>() * Constants.DefaultValidationLayers.Length);
            Marshal.Copy(Constants.DefaultValidationLayers.Select(Marshal.StringToHGlobalAnsi).ToArray(), 0,
                         enabledLayersPtr,
                         Constants.DefaultValidationLayers.Length);

            vkInstanceInfo.EnabledLayerCount = (uint)Constants.DefaultValidationLayers.Length;
            vkInstanceInfo.PpEnabledLayerNames = (byte**)enabledLayersPtr;

            var res = _vk.CreateInstance(vkInstanceInfo, null, out var instance);
            if (res != Result.Success)
                throw new PlatformException("Vulkan instance creation failed.", new VulkanException(res));

            for (var i = 0; i < Constants.DefaultValidationLayers.Length; i++)
            {
                var ptr = Marshal.ReadIntPtr(enabledLayersPtr + i * Marshal.SizeOf<IntPtr>());
                Marshal.FreeHGlobal(ptr);
            }

            Marshal.FreeHGlobal(enabledLayersPtr);
#else
            vkInstanceInfo.EnabledLayerCount = 0;
            _vk = Vk.GetApi(vkInstanceInfo, out var instance);
#endif
            ApplicationInfo.FreeVkAppInfo(vkAppInfo);

            return new VulkanInstance(instance);
        }
    }

    public IReadOnlyCollection<string> GetAvailableValidationLayers()
    {
        unsafe
        {
            // ReSharper disable once ConvertToConstant.Local
            uint layerCount = 0;
            var res = _vk.EnumerateInstanceLayerProperties(ref layerCount, null);
            if (res != Result.Success)
                return Array.Empty<string>();

            var availableLayers = new LayerProperties[layerCount];
            res = _vk.EnumerateInstanceLayerProperties(layerCount.ToSpan(), availableLayers);

            return res != Result.Success
                       ? ImmutableArray<string>.Empty
                       : availableLayers.Select(properties => new string((sbyte*)properties.LayerName))
                                        .ToImmutableArray();
        }
    }

    public IEnumerable<ExtensionProperties> GetAvailableExtensionProperties()
    {
        // ReSharper disable once ConvertToConstant.Local
        uint count = 0;
        _vk.EnumerateInstanceExtensionProperties(ReadOnlySpan<byte>.Empty, count.ToSpan(),
                                                 Span<ExtensionProperties>.Empty);

        var extensions = new ExtensionProperties[count];
        _vk.EnumerateInstanceExtensionProperties(Array.Empty<byte>(), count.ToSpan(), extensions);

        return extensions;
    }

    private bool CheckValidationLayersSupport(IEnumerable<string> validationLayers)
    {
        var layerNames = GetAvailableValidationLayers();
        Console.WriteLine($"Available validation layers : {layerNames.Aggregate((sl, sr) => sl + ", " + sr)}");

        return layerNames
               .Intersect(validationLayers)
               .Any();
    }
}