using GameDotNet.Editor.ViewModels;
using GameDotNet.Editor.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Editor.Tools;

public static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddEditorViews(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddTransient<WebGpuViewModel>()
            .AddView<WebGpuViewModel, WebGpuView>()
            .AddSingleton<EntityTreeViewModel>()
            .AddView<EntityTreeViewModel, EntityTreeViewControl>()
            .AddSingleton<LogViewerViewModel>()
            .AddView<LogViewerViewModel, LogViewerControl>()
            .AddSingleton<MainWindowViewModel>()
            .AddView<EntityInspectorViewModel, EntityInspectorControl>()
            .AddSingleton<EntityInspectorViewModel>()
            
            .AddPooled<PropertyNodeViewModel>()
            .AddSingleton<PropertyNodeCache>();
    }
}