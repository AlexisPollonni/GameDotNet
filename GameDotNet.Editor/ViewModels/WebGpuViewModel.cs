using System.Threading;
using System.Threading.Tasks;
using GameDotNet.Graphics;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Management.ECS;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.ViewModels;

public class WebGpuViewModel : ViewModelBase
{
    private readonly ILogger<WebGpuViewModel> _logger;
    private readonly Universe _universe;
    private readonly NativeViewManager _viewManager;

    public WebGpuViewModel(ILogger<WebGpuViewModel> logger, Universe universe, NativeViewManager viewManager)
    {
        _logger = logger;
        _universe = universe;
        _viewManager = viewManager;
    }

    public void SetMainView(INativeView view)
    {
        _viewManager.MainView = view;
    }

    public async Task Initialize(CancellationToken token = default)
    {
        await _universe.Initialize(token);

        Task.Run(() =>
        {
            while (true)
                _universe.Update();
        });
    }

    public void Update()
    {
        _universe.Update();
    }
}