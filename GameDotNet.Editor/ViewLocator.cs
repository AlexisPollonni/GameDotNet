using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace GameDotNet.Editor;

// From  https://github.com/AvaloniaUI/Avalonia.Samples
internal sealed class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control>> _dic;

    public ViewLocator(IEnumerable<ViewLocationDescriptor> descriptors)
    {
        _dic = descriptors.ToDictionary(x => x.ViewModel, x => x.Factory);
    }

    public Control Build(object? param) => _dic[param!.GetType()]();

    public bool Match(object? param) => param is not null && _dic.ContainsKey(param.GetType());

    public sealed record ViewLocationDescriptor(Type ViewModel, Func<Control> Factory);
}