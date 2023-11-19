using System.Threading;
using System.Threading.Tasks;
using GameDotNet.Management.ECS;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Editor.ViewModels;

public class WebGpuViewModel : ViewModelBase
{
    private readonly ILogger<WebGpuViewModel> _logger;
    private readonly Universe _universe;

    public WebGpuViewModel(ILogger<WebGpuViewModel> logger, Universe universe)
    {
        _logger = logger;
        _universe = universe;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _universe.Initialize(token);

        await Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
                _universe.Update();
        }, token);
    }
}