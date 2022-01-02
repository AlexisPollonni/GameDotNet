using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Core.Tools;
using Core.Tools.Extensions;
using Silk.NET.Core;
using Silk.NET.Core.Loader;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Core.Graphics.Vulkan.Bootstrap;

public class InstanceBuilder
{
    public static readonly unsafe DebugUtilsMessengerCallbackFunctionEXT DefaultDebugMessenger =
        (severity, types, data, userData) =>
        {
            var s = severity switch
            {
                DebugUtilsMessageSeverityFlagsEXT
                    .DebugUtilsMessageSeverityErrorBitExt => "ERROR",
                DebugUtilsMessageSeverityFlagsEXT
                    .DebugUtilsMessageSeverityWarningBitExt => "WARNING",
                DebugUtilsMessageSeverityFlagsEXT
                    .DebugUtilsMessageSeverityInfoBitExt => "INFO",
                DebugUtilsMessageSeverityFlagsEXT
                    .DebugUtilsMessageSeverityVerboseBitExt => "VERBOSE",
                _ => "UNKNOWN"
            };

            var t = types switch
            {
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                    DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt |
                    DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt =>
                    "General | Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt |
                    DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt => "Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                    DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt => "General | Performance",
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt => "Performance",
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                    DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt => "General | Validation",
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt => "Validation",
                DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt => "General",
                _ => "Unknown"
            };

            var msg = Marshal.PtrToStringAnsi((nint)data->PMessage);

            Debug.WriteLine($"[{s}: {t}]\n{msg}\n");

            return Vk.False;
        };

    public InstanceBuilder()
    {
        Layers = Enumerable.Empty<string>();
        Extensions = Enumerable.Empty<string>();
        DisabledValidationChecks = new List<ValidationCheckEXT>();
        EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>();
        DisabledValidationFeatures = new List<ValidationFeatureDisableEXT>();
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
    public IList<ValidationCheckEXT> DisabledValidationChecks { get; set; }
    public IList<ValidationFeatureEnableEXT> EnabledValidationFeatures { get; set; }
    public IList<ValidationFeatureDisableEXT> DisabledValidationFeatures { get; set; }

    public DebugUtilsMessengerCallbackFunctionEXT? DebugCallback { get; set; }
    public DebugUtilsMessageSeverityFlagsEXT? DebugMessageSeverity { get; set; }
    public DebugUtilsMessageTypeFlagsEXT? DebugMessageType { get; set; }

    public InstanceBuilder UseDefaultDebugMessenger()
    {
        DebugCallback = DefaultDebugMessenger;
        return this;
    }

    [SuppressMessage("ReSharper", "RedundantCast")]
    public VulkanInstance Build()
    {
        var disposables = new CompositeDisposable();
        try
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
                        apiVersion = queriedApiVersion >= DesiredApiVersion
                                         ? DesiredApiVersion.Value
                                         : queriedApiVersion;
                    }
                }

                Console.WriteLine($"Chose Vulkan version {(Version)apiVersion}"); //TODO: add proper logging

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

                CreateAppInfo(out var appInfo, apiVersion).DisposeWith(disposables);

                CreateInstanceInfo(out var vkInstanceInfo, extensions, layers, appInfo).DisposeWith(disposables);

                var res2 = vk.CreateInstance(in vkInstanceInfo, null, out var instance);

                if (res2 != Result.Success)
                    throw new PlatformException("Failed to bootstrap vulkan instance", new VulkanException(res2));

                return new(instance);
            }
        }
        finally
        {
            disposables.Dispose();
        }
    }

    private unsafe IDisposable CreateAppInfo(out Silk.NET.Vulkan.ApplicationInfo info, Version32 apiVersion)
    {
        var appName = SilkMarshal.StringToMemory(ApplicationName);
        var engineName = SilkMarshal.StringToMemory(EngineName);

        info = new()
        {
            SType = StructureType.ApplicationInfo,
            PNext = null,
            ApiVersion = apiVersion,
            ApplicationVersion = ApplicationVersion ?? new Version32(0, 0, 1),
            EngineVersion = Constants.EngineVersion,
            PApplicationName = appName.AsPtr<byte>(),
            PEngineName = engineName.AsPtr<byte>()
        };

        return new CompositeDisposable(appName, engineName);
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
                              DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityInfoBitExt,
            MessageType = DebugMessageType ??
                          DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                          DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt,
            PfnUserCallback = new(DebugCallback),
            PUserData = null
        };
    }

    private unsafe IDisposable? CreateValidationFeatures(out ValidationFeaturesEXT? features)
    {
        features = null;
        if (EnabledValidationFeatures.Count == 0 && DisabledValidationFeatures.Count == 0)
            return null;

        var enabled = EnabledValidationFeatures.ToGlobalMemory();
        var disabled = DisabledValidationFeatures.ToGlobalMemory();

        features = new()
        {
            SType = StructureType.ValidationFeaturesExt,
            PNext = null,
            EnabledValidationFeatureCount = (uint)EnabledValidationFeatures.Count,
            DisabledValidationFeatureCount = (uint)DisabledValidationFeatures.Count,
            PEnabledValidationFeatures = enabled.AsPtr<ValidationFeatureEnableEXT>(),
            PDisabledValidationFeatures = disabled.AsPtr<ValidationFeatureDisableEXT>()
        };

        return new CompositeDisposable(enabled, disabled);
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
                                                  IReadOnlyList<string> layers, Silk.NET.Vulkan.ApplicationInfo appInfo)
    {
        var memAppInfo = appInfo.ToGlobalMemory();
        var memExtensions = SilkMarshal.StringArrayToMemory(extensions);
        var memLayers = SilkMarshal.StringArrayToMemory(layers);

        info = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PNext = null,
            PApplicationInfo = memAppInfo.AsPtr<Silk.NET.Vulkan.ApplicationInfo>(),
            EnabledExtensionCount = (uint)extensions.Count,
            EnabledLayerCount = (uint)layers.Count,
            PpEnabledExtensionNames = (byte**)memExtensions.Handle,
            PpEnabledLayerNames = (byte**)memLayers.Handle,
            Flags = 0
        };

        CreateDebugMessengerInfo(out var messengerInfo);
        var d1 = CreateValidationFeatures(out var features);
        var d2 = CreateValidationFlags(out var checks);

        var memMessengerInfo = messengerInfo?.ToGlobalMemory();
        var memFeatures = features?.ToGlobalMemory();
        var memChecks = checks?.ToGlobalMemory();

        var pNextChain = new[] { memMessengerInfo, memFeatures, memChecks }.WhereNotNull().ToArray();
        SetupPNextChain(pNextChain);

        if (pNextChain.Length > 0)
            info.PNext = (void*)pNextChain[0].Handle;

        return new CompositeDisposable(new[]
        {
            memAppInfo, memExtensions, memLayers, d1, d2, memMessengerInfo, memFeatures, memChecks
        }.WhereNotNull());
    }

    private static unsafe void SetupPNextChain(params GlobalMemory[] structs)
    {
        if (structs.Length <= 1)
            return;

        for (var i = 0; i < structs.Length - 1; i++)
        {
            structs[i].AsRef<BaseOutStructure>().PNext = structs[i + 1].AsPtr<BaseOutStructure>();
        }
    }
}