using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using GameDotNet.Editor.Services;
using GameDotNet.Editor.Tools;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Editor.Views;
using GameDotNet.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Simple.Avalonia.Hosting;

namespace GameDotNet.Editor;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Engine.CreateBuilder(args, "GameDotNet-Editor");

        builder.AddEngineFileLogger()
               .AddAvaloniaDesktopHost<MainWindow>(BuildAvaloniaAppFromServiceProvider)
               .Services.AddTransient<IScheduler>(provider =>
               {
                   provider.GetRequiredService<IHostApplicationLifetime>()
                           .ApplicationStarted.IsCancellationRequested
                           .ShouldBeTrue("Scheduler requested before application started");
                   return AvaloniaScheduler.Instance;
               }); //TODO: Change when moving to R3

        builder.Services
               .AddEngineInstrumentation()
               .AddAvaloniaLogger(LogEventLevel.Warning, LogArea.Property, LogArea.Control, LogArea.Visual, LogArea.Layout, LogArea.Binding, LogArea.Platform, LogArea.Win32Platform)
               .AddTransient<ViewLocator>()
               .AddEditorViews()
               .AddGameDotNetGraphicsAvalonia()
               .AddGameDotNetEditor()
               
               .AddViewerLogging();
        
        
        builder.Build().Run();
    }

    /// <summary>
    /// Only used by the visual designer in <see cref="BuildAvaloniaApp"/>
    /// </summary>
    private static readonly IServiceProvider EmptyServiceProvider = new ServiceCollection().BuildServiceProvider();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain / Setup is called: things aren't initialized
    // yet and stuff might break.

    // Avalonia configuration, don't remove; also used by visual designer.
    // ReSharper disable once UnusedMember.Global
    public static AppBuilder BuildAvaloniaApp() =>
        BuildAvaloniaAppFromServiceProvider(EmptyServiceProvider);

    private static AppBuilder BuildAvaloniaAppFromServiceProvider(IServiceProvider serviceProvider) =>
        AppBuilder.Configure(() => new App(serviceProvider))
                  .UsePlatformDetect()
                  .UseReactiveUI()
                  .AfterSetup(builder =>
                  {
                      // The ApplicationLifetime is null when using the previewer.
                      if (builder.Instance?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                      {
                          AfterDesktopSetup(desktop, serviceProvider);
                      }
                  });

    private static void AfterDesktopSetup(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider provider)
    {
        var mainWindow = provider.GetRequiredService<Window>();
        var mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = mainWindowViewModel;
        desktop.MainWindow = mainWindow;
        
        Logger.Sink = provider.GetRequiredService<ILogSink>();

        Application.Current?.DataTemplates.Add(provider.GetRequiredService<ViewLocator>());
        
        desktop.MainWindow.DataContext = provider.GetRequiredService<MainWindowViewModel>();
    }
}