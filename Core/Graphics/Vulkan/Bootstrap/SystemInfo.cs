using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan.Bootstrap;

public class SystemInfo
{
    private readonly Vk _vk;

    public SystemInfo()
    {
        _vk = Vk.GetApi();

        AvailableLayers = FetchLayers();
        AvailableExtensions = FetchExtensions();

        IsValidationLayersEnabled = Constants.DefaultValidationLayers.All(IsLayerAvailable);
        IsDebugUtilsAvailable = IsExtensionAvailable(Constants.DebugUtilsExtensionName);

        foreach (var layer in AvailableLayers)
        {
            unsafe
            {
                var layerExtensions = FetchExtensions(Marshal.PtrToStringAuto((IntPtr)layer.LayerName));

                IsDebugUtilsAvailable = !IsDebugUtilsAvailable && layerExtensions.Any(properties =>
                                            Marshal.PtrToStringAuto((IntPtr)properties
                                                                        .ExtensionName) ==
                                            Constants.DebugUtilsExtensionName);
            }
        }
    }

    public IEnumerable<LayerProperties> AvailableLayers { get; }
    public IEnumerable<ExtensionProperties> AvailableExtensions { get; }

    public bool IsValidationLayersEnabled { get; }
    public bool IsDebugUtilsAvailable { get; }

    public bool IsLayerAvailable(string name)
    {
        return AvailableLayers.Any(properties =>
        {
            unsafe
            {
                return Marshal.PtrToStringAuto((IntPtr)properties.LayerName) == name;
            }
        });
    }

    public bool IsExtensionAvailable(string name)
    {
        return AvailableExtensions.Any(properties =>
        {
            unsafe
            {
                return Marshal.PtrToStringAuto((IntPtr)properties.ExtensionName) == name;
            }
        });
    }

    private IEnumerable<LayerProperties> FetchLayers()
    {
        // ReSharper disable once ConvertToConstant.Local
        uint layerCount = 0;
        var res = _vk.EnumerateInstanceLayerProperties(ref layerCount, ref Unsafe.NullRef<LayerProperties>());
        if (res != Result.Success)
            return Enumerable.Empty<LayerProperties>();

        var availableLayers = new LayerProperties[layerCount];
        res = _vk.EnumerateInstanceLayerProperties(ref layerCount, ref availableLayers.DangerousGetReference());

        return res != Result.Success ? Enumerable.Empty<LayerProperties>() : availableLayers;
    }

    private IEnumerable<ExtensionProperties> FetchExtensions(string? layerName = null)
    {
        uint extensionCount = 0;
        var res = _vk.EnumerateInstanceExtensionProperties(layerName, ref extensionCount,
                                                           ref Unsafe.NullRef<ExtensionProperties>());
        if (res != Result.Success)
            return Enumerable.Empty<ExtensionProperties>();

        var availableExtensions = new ExtensionProperties[extensionCount];
        res = _vk.EnumerateInstanceExtensionProperties(layerName, ref extensionCount,
                                                       ref availableExtensions.DangerousGetReference());

        return res != Result.Success ? Enumerable.Empty<ExtensionProperties>() : availableExtensions;
    }
}