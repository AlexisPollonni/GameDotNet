using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using VkAppInfo = Silk.NET.Vulkan.ApplicationInfo;

namespace Core.Graphics.Vulkan;

public sealed class ApplicationInfo
{
    private unsafe VkAppInfo* _appInfo;

    public ApplicationInfo(string applicationName, Version32 apiVersion, Version32 applicationVersion)
    {
        ApplicationName = applicationName;
        ApiVersion = apiVersion;
        ApplicationVersion = applicationVersion;
    }

    public string ApplicationName { get; }
    public Version32 ApiVersion { get; }
    public Version32 ApplicationVersion { get; }

    unsafe ~ApplicationInfo()
    {
        FreeVkAppInfo(_appInfo);
    }

    internal unsafe VkAppInfo* ToVkAppInfo()
    {
        FreeVkAppInfo(_appInfo);

        var s = new VkAppInfo
        {
            SType = StructureType.ApplicationInfo,
            PNext = null,
            ApiVersion = ApiVersion,
            ApplicationVersion = ApplicationVersion,
            EngineVersion = Constants.EngineVersion,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(ApplicationName),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi(Constants.EngineName)
        };

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(s));
        Marshal.StructureToPtr(s, ptr, false);

        _appInfo = (VkAppInfo*)ptr;
        return _appInfo;
    }

    internal static unsafe void FreeVkAppInfo(VkAppInfo* info)
    {
        Marshal.DestroyStructure<VkAppInfo>((IntPtr)info);
        Marshal.FreeHGlobal((IntPtr)info);
    }
}