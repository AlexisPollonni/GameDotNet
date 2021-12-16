using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Vulkan;

using VkAppInfo = Silk.NET.Vulkan.ApplicationInfo;

namespace Core.Graphics.Vulkan;

public class ApplicationInfo
{
    public string ApplicationName { get; }
    public Version32 ApiVersion { get; }
    public Version32 ApplicationVersion { get; }


    public ApplicationInfo(string applicationName, Version32 apiVersion, Version32 applicationVersion)
    {
        ApplicationName = applicationName;
        ApiVersion = apiVersion;
        ApplicationVersion = applicationVersion;
    }

    internal unsafe VkAppInfo* ToVkAppInfo()
    {
        var s = new VkAppInfo
        {
            SType = StructureType.ApplicationInfo,
            ApiVersion = ApiVersion,
            ApplicationVersion = ApplicationVersion,
            EngineVersion = Constants.EngineVersion,
            PApplicationName = (byte*) Marshal.StringToHGlobalAuto(ApplicationName),
            PEngineName = (byte*) Marshal.StringToHGlobalAuto(Constants.EngineName)
        };

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(s));
        Marshal.StructureToPtr(s, ptr, false);

        return (VkAppInfo*) ptr;
    }

    internal static unsafe void FreeVkAppInfo(VkAppInfo* info)
    {
        Marshal.DestroyStructure<VkAppInfo>((IntPtr) info);
        Marshal.FreeHGlobal((IntPtr) info);
    }
}