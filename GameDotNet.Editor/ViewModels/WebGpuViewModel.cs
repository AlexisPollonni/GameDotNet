using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using GameDotNet.Graphics;
using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Graphics.WGPU;
using GameDotNet.Management.ECS;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GameDotNet.Editor.ViewModels;

public class WebGpuViewModel : ViewModelBase
{
    [ObservableAsProperty]
    public TimelineStats RenderStats { get; }
    
    private readonly ILogger<WebGpuViewModel> _logger;
    private readonly Universe _universe;
    private readonly AssimpNetImporter _importer;
    private readonly WebGpuRenderSystem _renderSystem;

    public WebGpuViewModel(ILogger<WebGpuViewModel> logger, Universe universe, AssimpNetImporter importer, WebGpuRenderSystem renderSystem)
    {
        _logger = logger;
        _universe = universe;
        _importer = importer;
        _renderSystem = renderSystem;
        
        this.WhenActivated(d =>
        {
            Observable.Interval(TimeSpan.FromSeconds(0.5))
                      .ObserveOn(Scheduler.Default)
                      .Select(_ => renderSystem.RenderTimings.ComputeStats())
                      .ToPropertyEx(this, vm => vm.RenderStats)
                      .DisposeWith(d);
        });
    }
}