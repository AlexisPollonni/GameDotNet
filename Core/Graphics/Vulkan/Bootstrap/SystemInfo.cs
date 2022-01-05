using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Toolkit.HighPerformance;
using Serilog;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace Core.Graphics.Vulkan.Bootstrap;

public class SystemInfo
{
    private readonly ILogger _logger;
    private readonly Vk _vk;

    public SystemInfo()
    {
        _vk = Vk.GetApi();
        _logger = Log.ForContext<SystemInfo>();

        AvailableLayers = FetchLayers().ToImmutableArray();
        AvailableExtensions = FetchExtensions().ToImmutableArray();

        IsValidationLayersEnabled = Constants.DefaultValidationLayers.All(IsLayerAvailable);
        IsDebugUtilsAvailable = _vk.IsInstanceExtensionPresent(ExtDebugUtils.ExtensionName);


        foreach (var layer in AvailableLayers)
        {
            var layerExtensions = FetchExtensions(layer.GetLayerName());

            IsDebugUtilsAvailable = IsDebugUtilsAvailable || layerExtensions.Any(properties =>
                                        properties.GetExtensionName() == ExtDebugUtils.ExtensionName);
        }

        _logger.Information("Available Vulkan layers: {VulkanLayers}", AvailableLayers.Select(p => p.GetLayerName()));
        _logger.Information("Available Vulkan extensions: {VulkanExtensions}",
                            AvailableExtensions.Select(e => e.GetExtensionName()));
        _logger.Information("Vulkan validation layers available: {ValidationAvailable}", IsValidationLayersEnabled);
        _logger.Information("Vulkan debug utils available: {UtilsAvailable}", IsDebugUtilsAvailable);
    }

    public IReadOnlyList<LayerProperties> AvailableLayers { get; }
    public IReadOnlyList<ExtensionProperties> AvailableExtensions { get; }

    public bool IsValidationLayersEnabled { get; }
    public bool IsDebugUtilsAvailable { get; }

    public bool IsLayerAvailable(string name) => AvailableLayers.Any(properties => properties.GetLayerName() == name);

    public bool IsExtensionAvailable(string name) =>
        AvailableExtensions.Any(properties => properties.GetExtensionName() == name);

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