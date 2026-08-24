using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.ILGPU.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Graphics.ILGPU.Tools;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddIlGpuRenderer()
        {
            return services
                .AddSingleton<RenderHost>()
                .AddSingleton<IEntityRenderer, EntityRenderer>();
        }
    }
}
