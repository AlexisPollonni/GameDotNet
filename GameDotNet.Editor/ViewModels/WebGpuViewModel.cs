using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using GameDotNet.Graphics;
using GameDotNet.Graphics.WGPU;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GameDotNet.Editor.ViewModels;

public class WebGpuViewModel : ViewModelBase
{
    [ObservableAsProperty]
    public TimelineStats RenderStats { get; }

    public WebGpuViewModel(WebGpuRenderSystem renderSystem)
    {
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