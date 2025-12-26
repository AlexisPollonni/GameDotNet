using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;
using System.Reactive.Linq;
using Nito.Disposables;
using Disposable = Nito.Disposables.Disposable;

namespace GameDotNet.Editor.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel, IDisposable
{
    public ViewModelActivator Activator { get; } = new();

    public ViewModelBase()
    {
        this.WhenActivated(OnActivated);
    }
        
    public virtual void OnActivated(CompositeDisposable disposable)
    {
            
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Activator.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}