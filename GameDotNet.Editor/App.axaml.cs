using Avalonia.Markup.Xaml;
using Application = Avalonia.Application;

namespace GameDotNet.Editor;

public partial class App(IServiceProvider provider) : Application
{
    public IServiceProvider Provider { get; } = provider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static IServiceProvider? GetServiceProvider() => ((App?)Current)?.Provider;
}