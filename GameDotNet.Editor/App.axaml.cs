using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using GameDotNet.Editor.Tools;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Editor.Views;
using GameDotNet.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Application = Avalonia.Application;

namespace GameDotNet.Editor;

public partial class App : Application
{
    public IHost? GlobalHost { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var builder = Hosting.Application.CreateHostBuilder(Hosting.Application.CreateLogger(Current?.Name ?? "GameDotNet-Editor"));

        builder.Services
               .AddAvaloniaLogger(LogEventLevel.Debug, LogArea.Binding, LogArea.Platform, LogArea.Win32Platform)
               .AddCoreSystemServices()
               .AddTransient<ViewLocator>()
               .AddTransient<WebGpuViewModel>()
               .AddView<WebGpuViewModel, WebGpuView>()
               .AddTransient<MainWindowViewModel>();

        GlobalHost = builder.Build();

        Logger.Sink = GlobalHost.Services.GetRequiredService<ILogSink>();
            
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = GlobalHost.Services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) =>
            {
                GlobalHost.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                GlobalHost.Dispose();
                GlobalHost = null;
            };
        }
            
        DataTemplates.Add(GlobalHost.Services.GetRequiredService<ViewLocator>());

        base.OnFrameworkInitializationCompleted();
            
        // Usually, we don't want to block main UI thread.
        // But if it's required to start async services before we create any window,
        // then don't set any MainWindow, and simply call Show() on a new window later after async initialization. 
        await GlobalHost.StartAsync();
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