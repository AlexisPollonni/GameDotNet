using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace GameDotNet.Editor.Tools;

public class ImplementationDataTemplate : IDataTemplate, IRecyclingDataTemplate
{
    [Content]
    public DataTemplates AvailableTemplates { get; } = [];

    public Control? Build(object? data)
    {
        var template = AvailableTemplates.First(template => template.Match(data));

        return template.Build(data);
    }

    public Control? Build(object? data, Control? existing)
    {
        var template = AvailableTemplates.First(template => template.Match(data));
        if (template is IRecyclingDataTemplate recycling)
            return recycling.Build(data, existing);
        
        return template.Build(data);
    }

    public bool Match(object? data)
    {
        if (data is null)
            return false;

        return AvailableTemplates.Any(template => template.Match(data));
    }
}