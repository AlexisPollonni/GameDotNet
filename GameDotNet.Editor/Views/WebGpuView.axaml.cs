using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;
using ReactiveUI;

namespace GameDotNet.Editor.Views;

public partial class WebGpuView : ReactiveUserControl<WebGpuViewModel>
{
    public WebGpuView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            Observable.FromAsync(() => NativeControl.Initialize())
                      .SelectMany(async (_, token) =>
                      {
                          if(ViewModel is not null)
                            await ViewModel.Run(token);

                          return Unit.Default;
                      })
                      .Subscribe()
                      .DisposeWith(d);
        });
    }


}