using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Serilog;
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
public class InstanceBuilder
{
    private readonly Vk _api;

    public static readonly unsafe DebugUtilsMessengerCallbackFunctionEXT DefaultDebugMessenger =
        (severity, types, data, _) =>
        {
            var msgType = types switch
            {
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                    DebugUtilsMessageTypeFlagsEXT.ValidationBitExt =>
                    "General | Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt =>
                    "Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt =>
                    "General | Performance",
                DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "Performance",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt =>
                    "General | Validation",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt => "Validation",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt => "General",
                _ => "Unknown"
            };

            var msg = SilkMarshal.PtrToString((nint)data->PMessage);

            const string template = "<Vulkan || {MessageType}> {Message}";
            switch (severity)
            {
                case DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt:
                    Log.Debug(template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.InfoBitExt:
                    Log.Information(template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.WarningBitExt:
                    Log.Warning(template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt:
                    Log.Error(template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.None:
                default:
                    Log.Warning(template, msgType, msg);
                    break;
            }

            return Vk.False;
        };


    private readonly ILogger _logger;

    public InstanceBuilder(Vk api)
    {
        _api = api;
        _logger = Log.ForContext<InstanceBuilder>();
        Layers = Enumerable.Empty<string>();
        Extensions = Enumerable.Empty<string>();
        DisabledValidationChecks = new List<ValidationCheckEXT>();
        EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>();
        DisabledValidationFeatures = new List<ValidationFeatureDisableEXT>();
    }

    public InstanceBuilder() : this(Vk.GetApi())
    { }


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
    public IList<ValidationCheckEXT> DisabledValidationChecks { get; set; }
    public IList<ValidationFeatureEnableEXT> EnabledValidationFeatures { get; set; }
    public IList<ValidationFeatureDisableEXT> DisabledValidationFeatures { get; set; }

    public DebugUtilsMessengerCallbackFunctionEXT? DebugCallback { get; set; }
    public DebugUtilsMessageSeverityFlagsEXT? DebugMessageSeverity { get; set; }
    public DebugUtilsMessageTypeFlagsEXT? DebugMessageType { get; set; }
    public IVulkanAllocCallback AllocCallback { get; set; } = new NullAllocator();

    public InstanceBuilder UseDefaultDebugMessenger()
    {
        DebugCallback = DefaultDebugMessenger;
        return this;
    }

    public VulkanInstance Build()
    {
        using var d = new DisposableList();
        ref readonly var alloc = ref AllocCallback.Handle;

        var sysInfo = new SystemInfo();

        var apiVersion = ChooseApiVersion(_api);
        _logger.Information("Bootstrapper chose Vulkan version {VulkanVersion}", (Version)apiVersion);

        var supportsProperties2Ext = sysInfo.IsExtensionAvailable(KhrGetPhysicalDeviceProperties2.ExtensionName);

        var extensions = Extensions.ToList();
        if (DebugCallback is not null && sysInfo.IsDebugUtilsAvailable)
            extensions.Add(ExtDebugUtils.ExtensionName);

        if (apiVersion < Vk.Version11 && supportsProperties2Ext)
            extensions.Add(KhrGetPhysicalDeviceProperties2.ExtensionName);

        if (!IsHeadless)
        {
            bool CheckAddWindow(string name)
            {
                if (!sysInfo.IsExtensionAvailable(name)) return false;
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
                    addedWindowExtension = CheckAddWindow(KhrXlibSurface.ExtensionName) || addedWindowExtension;
                    addedWindowExtension =
                        CheckAddWindow(KhrWaylandSurface.ExtensionName) || addedWindowExtension;
                    break;
                case UnderlyingPlatform.Android:
                    addedWindowExtension = CheckAddWindow(KhrAndroidSurface.ExtensionName);
                    break;
                case UnderlyingPlatform.MacOS:
                case UnderlyingPlatform.IOS:
                    addedWindowExtension = CheckAddWindow("VK_EXT_metal_surface");
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

        CreateAppInfo(out var appInfo, apiVersion).DisposeWith(d);
        CreateInstanceInfo(out var vkInstanceInfo, extensions, layers, appInfo).DisposeWith(d);

        var res2 = _api.CreateInstance(in vkInstanceInfo, alloc, out var instance);
        if (res2 != Result.Success)
            throw new PlatformException("Failed to bootstrap vulkan instance", new VulkanException(res2));

        if (DebugCallback is null)
            return new(_api, instance, apiVersion, supportsProperties2Ext);

        CreateDebugMessengerInfo(out var messengerInfo);

        var info = messengerInfo!.Value;
        _api.TryGetInstanceExtension<ExtDebugUtils>(instance, out var debugUtilsExt);

        res2 = debugUtilsExt.CreateDebugUtilsMessenger(instance, in info, alloc, out var debugMessenger);
        if (res2 != Result.Success)
            throw new PlatformException("Couldn't create Vulkan debug messenger", new VulkanException(res2));

        return new(_api, instance, apiVersion, supportsProperties2Ext, IsValidationLayersEnabled, debugMessenger);
    }

    private Version32 ChooseApiVersion(Vk vk)
    {
        var apiVersion = Vk.Version10;

        if (RequiredApiVersion <= Vk.Version10 && DesiredApiVersion <= Vk.Version10)
            return apiVersion;

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
            apiVersion = queriedApiVersion >= DesiredApiVersion
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
            PEngineName = (EngineName ?? "").ToPtr(d)
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
            MessageSeverity = DebugMessageSeverity ??
                              DebugUtilsMessageSeverityFlagsEXT.InfoBitExt,
            MessageType = DebugMessageType ??
                          DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = new(DebugCallback),
            PUserData = null
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
            PEnabledValidationFeatures = EnabledValidationFeatures.AsPtr(d),
            PDisabledValidationFeatures = DisabledValidationFeatures.AsPtr(d)
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
            PDisabledValidationChecks = flags.AsPtr<ValidationCheckEXT>()
        };

        return flags;
    }

    private unsafe IDisposable CreateInstanceInfo(out InstanceCreateInfo info, IReadOnlyList<string> extensions,
                                                  IReadOnlyList<string> layers, ApplicationInfo appInfo)
    {
        var d = new DisposableList();

        info = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PNext = null,
            PApplicationInfo = appInfo.AsPtr(d),
            EnabledExtensionCount = (uint)extensions.Count,
            EnabledLayerCount = (uint)layers.Count,
            PpEnabledExtensionNames = extensions.ToByteDoublePtr(d),
            PpEnabledLayerNames = layers.ToByteDoublePtr(d),
            Flags = 0
        };

        CreateDebugMessengerInfo(out var messengerInfo);
        CreateValidationFeatures(out var features)?.DisposeWith(d);
        CreateValidationFlags(out var checks)?.DisposeWith(d);

        var pNextChain = new[]
            {
                messengerInfo?.ToGlobalMemory(),
                features?.ToGlobalMemory(),
                checks?.ToGlobalMemory()
            }.WhereNotNull()
             .Select(memory => memory.DisposeWith(d))
             .SetupPNextChain()
             .ToArray();

        if (pNextChain.Length > 0)
            info.PNext = (void*)pNextChain[0].Handle;

        return d;
    }
}