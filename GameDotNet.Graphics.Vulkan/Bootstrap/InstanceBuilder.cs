using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using GameDotNet.Core.Tooling.Collections;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Loader;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Bootstrap;

[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public partial class InstanceBuilder(IVulkanContext context)
{
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

    public IEnumerable<string> Layers { get; set; } = [];
    public IEnumerable<string> Extensions { get; set; } = [];
    public IList<ValidationCheckEXT> DisabledValidationChecks { get; set; } =
        new List<ValidationCheckEXT>();
    public IList<ValidationFeatureEnableEXT> EnabledValidationFeatures { get; set; } =
        new List<ValidationFeatureEnableEXT>();
    public IList<ValidationFeatureDisableEXT> DisabledValidationFeatures { get; set; } =
        new List<ValidationFeatureDisableEXT>();

    public DebugUtilsMessengerCallbackFunctionEXT? DebugCallback { get; set; }
    public DebugUtilsMessageSeverityFlagsEXT? DebugMessageSeverity { get; set; }
    public DebugUtilsMessageTypeFlagsEXT? DebugMessageType { get; set; }
    public IVulkanAllocCallback AllocCallback { get; set; } = new NullAllocator();

    public unsafe InstanceBuilder UseDefaultDebugMessenger(ILogger logger)
    {
        DebugCallback = (severity, types, data, _) =>
        {
            var msgType = types switch
            {
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                    | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
                    | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt =>
                    "General | Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                    | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                    | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "General | Performance",
                DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "Performance",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                    | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt => "General | Validation",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt => "Validation",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt => "General",
                _ => "Unknown",
            };

            var msg = SilkMarshal.PtrToString((nint)data->PMessage)?.ReplaceLineEndings(" ");

            var level = severity switch
            {
                DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt => LogLevel.Debug,
                DebugUtilsMessageSeverityFlagsEXT.InfoBitExt => LogLevel.Information,
                DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => LogLevel.Warning,
                DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => LogLevel.Error,
                _ => LogLevel.Warning,
            };

            LogVulkanMessage(logger, level, msgType, msg ?? "No message provided");

            return Vk.False;
        };
        return this;
    }

    [LoggerMessage("<Vulkan || {MessageType}> {Message}")]
    static partial void LogVulkanMessage(
        ILogger logger,
        LogLevel logLevel,
        string messageType,
        string message
    );

    public VulkanInstance Build()
    {
        using var d = new DisposableList();
        ref readonly var alloc = ref AllocCallback.Handle;

        var sysInfo = new SystemInfo();

        var apiVersion = ChooseApiVersion(context.Api);

        var supportsProperties2Ext = sysInfo.IsExtensionAvailable(
            KhrGetPhysicalDeviceProperties2.ExtensionName
        );

        var extensions = Extensions.ToList();
        if (DebugCallback is not null && sysInfo.IsDebugUtilsAvailable)
            extensions.Add(ExtDebugUtils.ExtensionName);

        if (apiVersion < Vk.Version11 && supportsProperties2Ext)
            extensions.Add(KhrGetPhysicalDeviceProperties2.ExtensionName);

        if (!IsHeadless)
        {
            bool CheckAddWindow(string name)
            {
                if (!sysInfo.IsExtensionAvailable(name))
                    return false;
                extensions.Add(name);
                return true;
            }

            var khrSurfaceAdded = CheckAddWindow(KhrSurface.ExtensionName);

            bool addedWindowExtension;
            switch (SearchPathContainer.Platform)
            {
                case UnderlyingPlatform.Windows64:
                case UnderlyingPlatform.Windows86:
                    addedWindowExtension = CheckAddWindow(KhrWin32Surface.ExtensionName);
                    break;
                case UnderlyingPlatform.Linux:
                    addedWindowExtension = CheckAddWindow(KhrXcbSurface.ExtensionName);
                    addedWindowExtension =
                        CheckAddWindow(KhrXlibSurface.ExtensionName) || addedWindowExtension;
                    addedWindowExtension =
                        CheckAddWindow(KhrWaylandSurface.ExtensionName) || addedWindowExtension;
                    break;
                case UnderlyingPlatform.Android:
                    addedWindowExtension = CheckAddWindow(KhrAndroidSurface.ExtensionName);
                    break;
                case UnderlyingPlatform.MacOS:
                case UnderlyingPlatform.IOS:
                    addedWindowExtension = CheckAddWindow(ExtMetalSurface.ExtensionName);
                    break;
                case UnderlyingPlatform.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!khrSurfaceAdded || !addedWindowExtension)
                throw new PlatformException("Couldn't load windowing extensions");
        }

        extensions = extensions.Distinct().ToList();
        var notSupported = extensions.Where(name => !sysInfo.IsExtensionAvailable(name)).ToArray();
        if (notSupported.Any())
            throw new PlatformException(
                $"Current platform doesn't support these extensions: {string.Join(",", notSupported)}"
            );

        var layers = Layers.ToList();
        if (
            IsValidationLayersEnabled
            || IsValidationLayersRequested && sysInfo.IsValidationLayersEnabled
        )
        {
            layers.AddRange(Constants.DefaultValidationLayers);

            const string layerSettingsExt = "VK_EXT_layer_settings";
            if (sysInfo.IsLayerExtensionAvailable("VK_LAYER_KHRONOS_validation", layerSettingsExt))
                extensions.Add(layerSettingsExt);
        }

        layers = layers.Distinct().ToList();
        notSupported = layers.Where(name => !sysInfo.IsLayerAvailable(name)).ToArray();
        if (notSupported.Any())
            throw new PlatformException(
                $"These requested layers are not available : {string.Join(",", notSupported)}"
            );

        CreateAppInfo(out var appInfo, apiVersion).DisposeWith(d);
        CreateInstanceInfo(out var vkInstanceInfo, extensions, layers, appInfo).DisposeWith(d);

        var res2 = context.Api.CreateInstance(in vkInstanceInfo, in alloc, out var instance);
        if (res2 != Result.Success)
            throw new PlatformException(
                "Failed to bootstrap vulkan instance",
                new VulkanException(res2)
            );

        if (DebugCallback is null)
            return new(context, instance, apiVersion, supportsProperties2Ext, extensions);

        CreateDebugMessengerInfo(out var messengerInfo);

        var info = messengerInfo!.Value;
        context.Api.TryGetInstanceExtension<ExtDebugUtils>(instance, out var debugUtilsExt);

        res2 = debugUtilsExt.CreateDebugUtilsMessenger(
            instance,
            in info,
            in alloc,
            out var debugMessenger
        );
        if (res2 != Result.Success)
            throw new PlatformException(
                "Couldn't create Vulkan debug messenger",
                new VulkanException(res2)
            );

        return new(
            context,
            instance,
            apiVersion,
            supportsProperties2Ext,
            extensions,
            IsValidationLayersEnabled,
            debugMessenger
        )
        {
            IsHeadless = IsHeadless,
        };
    }

    private Version32 ChooseApiVersion(Vk vk)
    {
        var apiVersion = Vk.Version10;

        if (RequiredApiVersion <= Vk.Version10 && DesiredApiVersion <= Vk.Version10)
            return apiVersion;

        var queriedApiVersion = Vk.Version10;
        var res = vk.EnumerateInstanceVersion(
            ref Unsafe.As<Version32, uint>(ref queriedApiVersion)
        );
        if (res != Result.Success && RequiredApiVersion is not null)
            throw new PlatformException(
                "Couldn't find vulkan api version",
                new VulkanException(res)
            );

        if (queriedApiVersion < RequiredApiVersion)
            throw new PlatformException(
                $"Vulkan version {(Version)RequiredApiVersion!} unavailable"
            );

        if (RequiredApiVersion > Vk.Version10)
        {
            apiVersion = queriedApiVersion;
        }
        else if (DesiredApiVersion > Vk.Version10)
        {
            apiVersion =
                queriedApiVersion >= DesiredApiVersion
                    ? DesiredApiVersion.Value
                    : queriedApiVersion;
        }

        return apiVersion;
    }

    private unsafe IDisposable CreateAppInfo(out ApplicationInfo info, Version32 apiVersion)
    {
        var d = new DisposableList();

        info = new()
        {
            SType = StructureType.ApplicationInfo,
            PNext = null,
            ApiVersion = apiVersion,
            ApplicationVersion = ApplicationVersion ?? new Version32(0, 0, 1),
            EngineVersion = Constants.EngineVersion,
            PApplicationName = (ApplicationName ?? "").ToPtr(d),
            PEngineName = (EngineName ?? "").ToPtr(d),
        };

        return d;
    }

    private void CreateDebugMessengerInfo(out DebugUtilsMessengerCreateInfoEXT? messenger)
    {
        messenger = null;
        if (DebugCallback is null)
            return;

        messenger = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            PNext = null,
            MessageSeverity = DebugMessageSeverity ?? DebugUtilsMessageSeverityFlagsEXT.InfoBitExt,
            MessageType =
                DebugMessageType
                ?? DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                    | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = new(DebugCallback),
            PUserData = null,
        };
    }

    private unsafe IDisposable? CreateValidationFeatures(out ValidationFeaturesEXT? features)
    {
        var d = new DisposableList();

        features = null;
        if (EnabledValidationFeatures.Count == 0 && DisabledValidationFeatures.Count == 0)
            return null;

        features = new()
        {
            SType = StructureType.ValidationFeaturesExt,
            PNext = null,
            EnabledValidationFeatureCount = (uint)EnabledValidationFeatures.Count,
            DisabledValidationFeatureCount = (uint)DisabledValidationFeatures.Count,
            PEnabledValidationFeatures = EnabledValidationFeatures.ToPtr(d),
            PDisabledValidationFeatures = DisabledValidationFeatures.ToPtr(d),
        };

        return d;
    }

    private unsafe IDisposable? CreateValidationFlags(out ValidationFlagsEXT? checks)
    {
        checks = null;
        if (DisabledValidationChecks.Count == 0)
            return null;

        var flags = DisabledValidationChecks.ToGlobalMemory();

        checks = new ValidationFlagsEXT
        {
            SType = StructureType.ValidationFlagsExt,
            PNext = null,
            DisabledValidationCheckCount = (uint)DisabledValidationChecks.Count,
            PDisabledValidationChecks = flags.AsPtr<ValidationCheckEXT>(),
        };

        return flags;
    }

    private unsafe IDisposable CreateInstanceInfo(
        out InstanceCreateInfo info,
        IReadOnlyList<string> extensions,
        IReadOnlyList<string> layers,
        ApplicationInfo appInfo
    )
    {
        var d = new DisposableList();

        info = new(
            pApplicationInfo: &appInfo,
            enabledExtensionCount: (uint)extensions.Count,
            enabledLayerCount: (uint)layers.Count,
            ppEnabledExtensionNames: extensions.ToByteDoublePtr(d),
            ppEnabledLayerNames: layers.ToByteDoublePtr(d),
            flags: 0
        );
        IChain<InstanceCreateInfo> infoChain = Chain.Create(info).DisposeWith(d);

        CreateDebugMessengerInfo(out var messengerInfo);
        CreateValidationFeatures(out var features)?.DisposeWith(d);
        CreateValidationFlags(out var checks)?.DisposeWith(d);

        if (messengerInfo is not null)
            AddExtension(messengerInfo.Value);

        if (features is not null)
            AddExtension(features.Value);

        if (checks is not null)
            AddExtension(checks.Value);

        if (IsValidationLayersEnabled || IsValidationLayersRequested)
        {
            var values = new uint[] { 5 };
            var layerSetting = new LayerSettingEXT(
                type: LayerSettingTypeEXT.Uint32Ext,
                pLayerName: "khronos_validation".ToPtr(d),
                pSettingName: "duplicate_message_limit".ToPtr(d),
                valueCount: 1,
                pValues: values.ToPtr(d)
            );
            var settingsInfo = new LayerSettingsCreateInfoEXT(
                settingCount: 1,
                pSettings: layerSetting.ToPtrPinned(d)
            );

            AddExtension(settingsInfo);
        }

        info = infoChain.Head;
        return d;

        void AddExtension<TExt>(TExt ext)
            where TExt : unmanaged, IExtendsChain<InstanceCreateInfo>
        {
            var nonGenericChain = (Chain)infoChain;

            infoChain = (IChain<InstanceCreateInfo>)nonGenericChain.AddAny(ext).DisposeWith(d);
        }
    }
}
