using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Vulkan;
using GameDotNet.Editor.Services;
using GameDotNet.Editor.Tools;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Editor.Views;
using GameDotNet.Graphics.ILGPU.Tools;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI.Avalonia;
using Shouldly;
using Simple.Avalonia.Hosting;

namespace GameDotNet.Editor;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Engine.CreateBuilder(args, "GameDotNet-Editor");

        builder
            .AddEngineFileLogger()
            .AddAvaloniaDesktopHost<MainWindow>(BuildAvaloniaAppFromServiceProvider)
            .Services.AddTransient<IScheduler>(provider =>
            {
                provider
                    .GetRequiredService<IHostApplicationLifetime>()
                    .ApplicationStarted.IsCancellationRequested.ShouldBeTrue(
                        "Scheduler requested before application started"
                    );
                return AvaloniaScheduler.Instance;
            });

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif
        //TODO: Change when moving to R3
        builder
            .Services.AddEngineInstrumentation()
            .AddAvaloniaLogger(
                LogEventLevel.Warning,
                LogArea.Property,
                LogArea.Control,
                LogArea.Visual,
                LogArea.Layout,
                LogArea.Binding,
                LogArea.Platform,
                LogArea.Win32Platform,
                LogArea.X11Platform,
                LogArea.LinuxFramebufferPlatform
            )
            .AddTransient<ViewLocator>()
            .AddGameDotNetGraphicsAvalonia()
            .AddVulkanBackend()
            .AddIlGpuRenderer()
            .AddGameDotNetEditor()
            .AddEditorViews()
            .AddViewerLogging();

        builder.Build().Run();
    }

    /// <summary>
    /// Only used by the visual designer in <see cref="BuildAvaloniaApp"/>
    /// </summary>
    private static readonly IServiceProvider EmptyServiceProvider =
        new ServiceCollection().BuildServiceProvider();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain / Setup is called: things aren't initialized
    // yet and stuff might break.

    // Avalonia configuration, don't remove; also used by visual designer.
    // ReSharper disable once UnusedMember.Global
    public static AppBuilder BuildAvaloniaApp() =>
        BuildAvaloniaAppFromServiceProvider(EmptyServiceProvider);

    private static AppBuilder BuildAvaloniaAppFromServiceProvider(IServiceProvider serviceProvider)
    {
        Logger.Sink = serviceProvider.GetRequiredService<ILogSink>();

        return AppBuilder
            .Configure(() => new App(serviceProvider))
            .UsePlatformDetect()
            .UseReactiveUI(builder => { })
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Vulkan] })
            .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Vulkan] })
            .With(
                new VulkanOptions
                {
                    CustomSharedDevice = serviceProvider.GetRequiredService<IVulkanDevice>(),
                }
            )
            .AfterSetup(builder =>
            {
                // The ApplicationLifetime is null when using the previewer.
                if (
                    builder.Instance?.ApplicationLifetime
                    is IClassicDesktopStyleApplicationLifetime desktop
                )
                {
                    AfterDesktopSetup(desktop, serviceProvider);
                }
            });
    }

    private static void AfterDesktopSetup(
        IClassicDesktopStyleApplicationLifetime desktop,
        IServiceProvider provider
    )
    {
        var mainWindow = provider.GetRequiredService<Window>();
        var mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = mainWindowViewModel;
        desktop.MainWindow = mainWindow;

        Application.Current?.DataTemplates.Add(provider.GetRequiredService<ViewLocator>());

        desktop.MainWindow.DataContext = provider.GetRequiredService<MainWindowViewModel>();
    }
}
