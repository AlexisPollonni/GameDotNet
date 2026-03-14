using Avalonia.Controls;
using GameDotNet.Editor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
using ServiceScan.SourceGenerator;

namespace GameDotNet.Editor.Tools;

public static partial class ServiceCollectionExtensions
{
    internal static IServiceCollection AddEditorViews(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddReactiveUserControls()
            .AddReactiveWindows()
            .AddPooled<PropertyNodeViewModel>()
            .AddSingleton<PropertyNodeCache>();
    }

    [GenerateServiceRegistrations(
        CustomHandler = nameof(AddView),
        AssignableTo = typeof(ReactiveUserControl<>)
    )]
    private static partial IServiceCollection AddReactiveUserControls(
        this IServiceCollection services
    );

    [GenerateServiceRegistrations(
        CustomHandler = nameof(AddView),
        AssignableTo = typeof(ReactiveWindow<>)
    )]
    private static partial IServiceCollection AddReactiveWindows(this IServiceCollection services);

    public static IServiceCollection AddView<TView, TViewModel>(this IServiceCollection services)
        where TView : Control, new()
        where TViewModel : ViewModelBase
    {
        services.AddSingleton(
            new ViewLocator.ViewLocationDescriptor(typeof(TViewModel), () => new TView())
        );
        return services;
    }
}
