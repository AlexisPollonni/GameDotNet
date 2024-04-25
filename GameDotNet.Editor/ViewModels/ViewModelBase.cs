using ReactiveUI;

namespace GameDotNet.Editor.ViewModels
{
    public class ViewModelBase : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; } = new();
    }
}