using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Core.Extensions;
using Silk.NET.Core;
using Silk.NET.Core.Loader;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using MemoryExtensions = Core.Extensions.MemoryExtensions;

namespace Core.Graphics.Vulkan.Bootstrap;

public class InstanceBuilder
{
    public InstanceBuilder()
    {
        Layers = Enumerable.Empty<string>();
        Extensions = Enumerable.Empty<string>();
    }

    public bool IsHeadless { get; set; }

    /// <summary>
    /// Enables validation layers, instance creation will throw an exception of type PlatformException if the validation layers are not available.
    /// </summary>
    public bool IsValidationLayersEnabled { get; set; }

    /// <summary>
    /// If true, checks if the validation layers are available and load them if they are. 
    /// </summary>
    public bool IsValidationLayersRequested { get; set; }

    public string? ApplicationName { get; set; }
    public string? EngineName { get; set; }

    public Version32? ApplicationVersion { get; set; }
    public Version32? EngineVersion { get; set; }
    public Version32? RequiredApiVersion { get; set; }
    public Version32? DesiredApiVersion { get; set; }

    public IEnumerable<string> Layers { get; set; }
    public IEnumerable<string> Extensions { get; set; }

    public DebugUtilsMessengerCallbackFunctionEXT? DebugCallback { get; set; }
    public DebugUtilsMessageSeverityFlagsEXT? DebugMessageSeverity { get; set; }
    public DebugUtilsMessageTypeFlagsEXT? DebugMessageType { get; set; }

    [SuppressMessage("ReSharper", "RedundantCast")]
    public VulkanInstance Build()
    {
        unsafe
        {
            var vk = Vk.GetApi();
            var sysInfo = new SystemInfo();

            var apiVersion = Vk.Version10;

            if (RequiredApiVersion > Vk.Version10 || DesiredApiVersion > Vk.Version10)
            {
                var queriedApiVersion = Vk.Version10;
                var res = vk.EnumerateInstanceVersion(ref Unsafe.As<Version32, uint>(ref queriedApiVersion));
                if (res != Result.Success && RequiredApiVersion is not null)
                    throw new PlatformException("Couldn't find vulkan api version", new VulkanException(res));

                if (queriedApiVersion < RequiredApiVersion)
                    throw new PlatformException($"Vulkan version {(Version)RequiredApiVersion!} unavailable");

                if (RequiredApiVersion > Vk.Version10)
                {
                    apiVersion = queriedApiVersion;
                }
                else if (DesiredApiVersion > Vk.Version10)
                {
                    apiVersion = queriedApiVersion >= DesiredApiVersion ? DesiredApiVersion.Value : queriedApiVersion;
                }
            }

            Console.WriteLine($"Chose Vulkan version {(Version)apiVersion}"); //TODO: add proper logging

            var appInfo =
                new ApplicationInfo(ApplicationName ?? "", apiVersion, ApplicationVersion ?? new Version32(0, 0, 1));

            var extensions = Extensions.ToList();
            if (DebugCallback is not null && sysInfo.IsDebugUtilsAvailable)
                extensions.Add(Constants.DebugUtilsExtensionName);

            if (apiVersion < Vk.Version11 &&
                sysInfo.IsExtensionAvailable(KhrGetPhysicalDeviceProperties2.ExtensionName))
                extensions.Add(KhrGetPhysicalDeviceProperties2.ExtensionName);

            if (!IsHeadless)
            {
                var checkAddWindow = (string name) =>
                {
                    if (!sysInfo.IsExtensionAvailable(name)) return false;
                    extensions.Add(name);
                    return true;
                };

                var khrSurfaceAdded = checkAddWindow(KhrSurface.ExtensionName);

                bool addedWindowExts;
                switch (SearchPathContainer.Platform)
                {
                    case UnderlyingPlatform.Windows64:
                    case UnderlyingPlatform.Windows86:
                        addedWindowExts = checkAddWindow(KhrWin32Surface.ExtensionName);
                        break;
                    case UnderlyingPlatform.Linux:
                        addedWindowExts = checkAddWindow(KhrXcbSurface.ExtensionName);
                        addedWindowExts = checkAddWindow(KhrXlibSurface.ExtensionName) || addedWindowExts;
                        addedWindowExts = checkAddWindow(KhrWaylandSurface.ExtensionName) || addedWindowExts;
                        break;
                    case UnderlyingPlatform.Android:
                        addedWindowExts = checkAddWindow(KhrAndroidSurface.ExtensionName);
                        break;
                    case UnderlyingPlatform.MacOS:
                    case UnderlyingPlatform.IOS:
                        addedWindowExts = checkAddWindow("VK_EXT_metal_surface");
                        break;
                    case UnderlyingPlatform.Unknown:
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!khrSurfaceAdded || !addedWindowExts)
                    throw new PlatformException("Couldn't load windowing extensions");
            }

            extensions = extensions.Distinct().ToList();
            var notSupported = extensions.Where(name => !sysInfo.IsExtensionAvailable(name)).ToArray();
            if (notSupported.Any())
                throw new
                    PlatformException($"Current platform doesn't support these extensions: {string.Join(",", notSupported)}");


            var layers = Layers.ToList();
            if (IsValidationLayersEnabled || IsValidationLayersRequested && sysInfo.IsValidationLayersEnabled)
                layers.AddRange(Constants.DefaultValidationLayers);

            layers = layers.Distinct().ToList();
            notSupported = layers.Where(name => !sysInfo.IsLayerAvailable(name)).ToArray();
            if (notSupported.Any())
                throw new
                    PlatformException($"These requested layers are not available : {string.Join(",", notSupported)}");


            var vkInstanceInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PNext = null,
                PApplicationInfo = appInfo.ToVkAppInfo(),
                EnabledExtensionCount = (uint)extensions.Count,
                EnabledLayerCount = (uint)layers.Count,
                PpEnabledExtensionNames = extensions.ToPtrStrArray(),
                PpEnabledLayerNames = layers.ToPtrStrArray()
            };

            var res2 = vk.CreateInstance(in vkInstanceInfo, null, out var instance);

            MemoryExtensions.FreePtrStrArray(vkInstanceInfo.PpEnabledExtensionNames,
                                             vkInstanceInfo.EnabledExtensionCount);
            MemoryExtensions.FreePtrStrArray(vkInstanceInfo.PpEnabledLayerNames, vkInstanceInfo.EnabledLayerCount);

            if (res2 != Result.Success)
                throw new PlatformException("Failed to bootstrap vulkan instance", new VulkanException(res2));

            return new VulkanInstance(instance);
        }
    }
}