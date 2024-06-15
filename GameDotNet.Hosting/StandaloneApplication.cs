using System.Reactive;
using System.Reactive.Linq;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Silk.NET.Windowing;

namespace GameDotNet.Hosting;

public sealed class StandaloneApplication : IDisposable
{
    public string ApplicationName { get; }
    public Engine Engine { get; }

    private readonly IView _mainView;
    private readonly DisposableList _disposal;
    private readonly MainThreadScheduler _mainScheduler;

    public StandaloneApplication(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name can't be null or empty", nameof(appName));
        ApplicationName = appName;

        _mainView = CreateSilkView();
        _disposal = new();
        _mainScheduler = new() { ExitIfEmpty = true };

        Engine = new(_mainScheduler);
        Engine.Builder.Services.AddEngineFileLogger(appName);

        Engine.OnInitialized.Subscribe(host =>
              {
                  var s = host.Services;
                  var eventFactory = s.GetRequiredService<EventFactory>();
                  s.GetRequiredService<NativeViewManager>().MainView = new SilkView(_mainView, eventFactory);
              })
              .DisposeWith(_disposal);

        var endObs = Observable.FromEvent(x => _mainView.Closing += x, x => _mainView.Closing -= x, _mainScheduler);
        
        //TODO: Move to a dedicated system (prob when adding support for multi views)
        Observable.Repeat(_mainView, _mainScheduler)
                  .TakeUntil(endObs)
                  .Subscribe(view =>
                  {
                      view.DoEvents();
                      if (!view.IsClosing) view.DoUpdate();
                      if (view.IsClosing) return;
                      view.DoRender();
                  })
                  .DisposeWith(_disposal);

        endObs
            .SelectMany(async (_, token) =>
              {
                  await Engine.Stop(token);
                  return Unit.Default;
              })
              .Subscribe()
              .DisposeWith(_disposal);
    }

    public async Task<int> Run(CancellationToken token = default)
    {
        _mainView.Initialize();

        var stTask = Engine.Start(token);

        _mainScheduler.Run();

        _mainView.DoEvents();
        _mainView.Reset();

        await stTask;
        await Engine.Stop(token);
        
        return 0;
    }

    public void Dispose()
    {
        Engine.Dispose();
        _mainView.Dispose();
        _disposal.Dispose();
        _mainScheduler.Dispose();

        Log.CloseAndFlush();
    }

    private static IView CreateSilkView()
    {
        Window.ShouldLoadFirstPartyPlatforms(true);
        IView view;
        Window.PrioritizeGlfw();

        var api = new GraphicsAPI(ContextAPI.None, new(1, 0));
        if (Window.IsViewOnly)
        {
            var opt = ViewOptions.Default;
            opt.API = api;
            view = Window.GetView(opt);
        }
        else
        {
            var opt = WindowOptions.Default;
            opt.API = api;
            opt.VSync = true;
            opt.Size = new(800, 600);
            opt.Title = "Test";

            view = Window.Create(opt);
        }

        return view;
    }
}