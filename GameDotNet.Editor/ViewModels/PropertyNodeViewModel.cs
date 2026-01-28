using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using DynamicData.Binding;
using GameDotNet.Editor.Tools;
using Microsoft.Extensions.ObjectPool;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GameDotNet.Editor.ViewModels;

[RegisterTransient(Registration =
    RegistrationStrategy.Self)] //NOTE: Transient might cause issues with disposal in the container, to investigate
internal sealed class PropertyNodeViewModel : ViewModelBase, IResettable, IEquatable<PropertyNodeViewModel>
{
    public PropertyNodeViewModel? Parent { get; set; }
    public string? Name { get; set; }
    public Type Type { get; set; } = typeof(object);

    public object? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    [Reactive] public bool IsExpanded { get; set; }
    public bool IsVisible => Parent?.IsExpanded ?? true;

    [Reactive] public bool IsReadonly { get; set; }
    public bool IsDirty { get; set; } = true;
    public ObservableCollectionExtended<PropertyNodeViewModel> Children { get; } = [];
    public SourceList<PropertyNodeViewModel> ChildPropertyNodes { get; }

    public SourceList<PropertyNodeViewModel>? ChildItemNodes
    {
        get => _childItemNodes;
        set
        {
            if (_childItemNodes is not null && value is null)
            {
                _subscription.Dispose();
                _subscription = ChildPropertyNodes.Connect().Bind(Children).Subscribe();
            }
            else if (_childItemNodes is null && value is not null)
            {
                _subscription.Dispose();
                _subscription = ChildPropertyNodes.Connect().Or(value.Connect()).Bind(Children).Subscribe();
            }

            _childItemNodes = value;
        }
    }


    public PropertyNodeViewModel(PropertyNodeCache cache)
    {
        _cache = cache;
        ChildPropertyNodes = new();

        _subscription = ChildPropertyNodes.Connect().Bind(Children).Subscribe();
    }

    private readonly PropertyNodeCache _cache;

    private object? _value;

    private IDisposable _subscription;

    private SourceList<PropertyNodeViewModel>? _childItemNodes;

    internal IEnumerable<PropertyNodeCache.PropertyCacheEntry> PropertyTypeEntries => _cache.GetDefaultEntries(Type);


    public bool TryReset()
    {
        Parent = null;
        Name = null;
        Type = typeof(object);
        _value = null;
        IsReadonly = false;
        IsDirty = true;
        ChildPropertyNodes.Clear();
        ChildItemNodes?.Clear();

        return true;
    }

    public void SetValueWithoutNotification(object? value) => _value = value;

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;

        _subscription.Dispose();
        _childItemNodes?.Dispose();
        ChildPropertyNodes.Dispose();

        base.Dispose(disposing);
    }

    public bool Equals(PropertyNodeViewModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Parent, other.Parent) &&
               Name == other.Name &&
               Type == other.Type &&
               Equals(_value, other._value);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PropertyNodeViewModel)obj);
    }
}