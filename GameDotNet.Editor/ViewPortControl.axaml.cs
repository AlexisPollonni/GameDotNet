using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GameDotNet.Editor;

public class ViewPortControl : UserControl
{
    public ViewPortControl()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    
}