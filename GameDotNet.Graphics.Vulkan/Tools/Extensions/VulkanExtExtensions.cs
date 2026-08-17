using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace GameDotNet.Graphics.Vulkan.Tools.Extensions;

public static class VulkanExtExtensions
{
    extension<TWrapped>(IVulkanWrapper<TWrapped> wrapped)
        where TWrapped : unmanaged
    {
        internal ExtShaderObject ShaderObjectExt =>
            wrapped.GetExtension<TWrapped, ExtShaderObject>();

        internal TExtension GetExtension<TExtension>()
            where TExtension : NativeExtension<Vk>
        {
            return wrapped.Context.GetExtension<TExtension>();
        }
    }

    public static T GetExtension<T>(this IVulkanContext context)
        where T : NativeExtension<Vk>
    {
        if (
            context.Api.TryGetInstanceExtension(context.Instance, out T ext)
            || context.Api.TryGetDeviceExtension(context.Instance, context.Device, out ext)
        )
            return ext;

        throw new InvalidOperationException(
            $"Extension {typeof(T).Name} is not available on this device."
        );
    }
}
