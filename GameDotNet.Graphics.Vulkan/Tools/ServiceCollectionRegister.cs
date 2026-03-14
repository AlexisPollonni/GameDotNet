using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;

namespace GameDotNet.Graphics.Vulkan.Tools;

public static class ServiceCollectionRegister
{
    public static unsafe IServiceCollection AddVulkanRenderer(
        this IServiceCollection services,
        byte[]? deviceLuid = null
    )
    {
        return services
            .AddSingleton<IVulkanContext, DefaultVulkanContext>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<DefaultVulkanContext>>();
                logger.LogInformation(
                    "Creating Vulkan context with device LUID: {Luid}",
                    deviceLuid != null ? BitConverter.ToString(deviceLuid) : "null"
                );

                var contextFac = provider.GetRequiredService<IVulkanContextFactory>();

                var context =
                    deviceLuid != null
                        ? contextFac.CreateFrom(deviceLuid)
                        : contextFac.CreateFrom((IVkSurfaceSource)null);

                if (context is not null)
                {
                    var props = context.PhysDevice.Properties;
                    logger.LogInformation(
                        "Created vulkan context with device {VulkanDevice}",
                        SilkMarshal.PtrToString((IntPtr)props.DeviceName)
                    );

                    return (DefaultVulkanContext)context;
                }

                throw new InvalidOperationException("Failed to create Vulkan context");
            })
            .AddSingleton<IEntityRenderer, VulkanRenderer>()
            .AddAutoFactories();
    }
}
