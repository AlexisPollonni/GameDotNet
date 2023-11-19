using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using GameDotNet.Editor.ViewModels;
using ReactiveUI;

namespace GameDotNet.Editor.Views;

public partial class WebGpuView : ReactiveUserControl<WebGpuViewModel>
{
    public WebGpuView()
    {
        InitializeComponent();

        this.WhenActivated(async d =>
        {
            await NativeControl.Initialize();
            await (ViewModel?.Initialize() ?? Task.CompletedTask);
        });
        
        
    }


}