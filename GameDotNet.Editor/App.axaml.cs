using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using GameDotNet.Editor.Tools;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Editor.Views;
using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Hosting;
using GameDotNet.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Application = Avalonia.Application;

namespace GameDotNet.Editor;

public partial class App : Application
{
    public Engine? Engine { get; private set; }
    public IHost? GlobalHost => Engine?.GlobalHost;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var logViewerVM = new LogViewerViewModel();
        var loggerConfig = Engine.CreateLoggerConfig(Current?.Name ?? "GameDotNet-Editor").WriteTo.LogViewSink(logViewerVM);
        var logger = Engine.CreateLogger(loggerConfig);

        Engine = new(logger, AvaloniaScheduler.Instance);

        Engine.Builder.Services
              .AddAvaloniaLogger(LogEventLevel.Debug, LogArea.Binding, LogArea.Platform, LogArea.Win32Platform)
              .AddSingleton<EditorUiUpdateSystem>()
              .AddTransient<ViewLocator>()
              .AddTransient<WebGpuViewModel>()
              .AddView<WebGpuViewModel, WebGpuView>()
              .AddSingleton<EntityTreeViewModel>()
              .AddView<EntityTreeViewModel, EntityTreeViewControl>()
              .AddSingleton(logViewerVM)
              .AddView<LogViewerViewModel, LogViewerControl>()
              .AddSingleton<MainWindowViewModel>()
              .AddView<EntityInspectorViewModel, EntityInspectorControl>()
              .AddSingleton<EntityInspectorViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            
            Engine.OnInitialized.Subscribe(host =>
            {
                var s = host.Services;
                Logger.Sink = s.GetRequiredService<ILogSink>();
                DataTemplates.Add(s.GetRequiredService<ViewLocator>());
                desktop.MainWindow.DataContext = s.GetRequiredService<MainWindowViewModel>();
                
                //TODO: Remove when asset manager and scene loading Ui is done
                s.GetRequiredService<AssimpNetImporter>().LoadSceneFromFile("Assets/MonkeyScene.dae", out var scene);
                s.GetRequiredService<SceneManager>().LoadScene(scene ?? throw new InvalidOperationException());
            });


            Observable.FromEventPattern<ShutdownRequestedEventArgs>(
                          desktop,
                          nameof(IClassicDesktopStyleApplicationLifetime.ShutdownRequested), Scheduler.Default)
                      .SelectMany(async (p, token) =>
                      {
                          await Engine.Stop(token);
                          return Unit.Default;
                      })
                      .Subscribe(_ =>
                      {
                          Engine.Dispose();
                          Engine = null;
                      });
        }


        base.OnFrameworkInitializationCompleted();

        // Usually, we don't want to block main UI thread.
        // But if it's required to start async services before we create any window,
        // then don't set any MainWindow, and simply call Show() on a new window later after async initialization. 
        await Engine.Start();
    }

    public static IHost? GetCurrentHost()
    {
        return ((App?)Current)?.GlobalHost;
    }

    public static IServiceProvider? GetServiceProvider()
    {
        return GetCurrentHost()?.Services;
    }
}