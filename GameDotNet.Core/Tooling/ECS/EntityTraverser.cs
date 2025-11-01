using Arch.Core;
using Arch.Relationships;
using GameDotNet.Core.Tooling.Extensions;
using ZLinq;

namespace GameDotNet.Core.ECS;

public struct EntityTraverser(Entity current) : ITraverser<EntityTraverser, Entity>
{
    private int _childIndex = 0;

    public Entity Origin { get; } = current;

    public void Dispose() { }

    public EntityTraverser ConvertToTraverser(Entity next) =>
        new(next);

    public bool TryGetHasChild(out bool hasChild)
    {
        ref var children = ref current.GetRelationships<ParentOf>();
        var enumerator = children.GetEnumerator();
        hasChild = enumerator.MoveNext();
        return true;
    }

    public bool TryGetChildCount(out int count)
    {
        ref var children = ref current.GetRelationships<ParentOf>();
        count = 0;
        var enumerator = children.GetEnumerator();
        while (enumerator.MoveNext())
        {
            count++;
        }

        return true;
    }

    public bool TryGetParent(out Entity parent)
    {
        ref var relations = ref current.GetRelationships<ChildOf>();
        var enumerator = relations.GetEnumerator();

        if (enumerator.MoveNext())
        {
            parent = enumerator.Current.Key;
            return true;
        }

        parent = default;
        return false;
    }

    public bool TryGetNextChild(out Entity child)
    {
        ref var children = ref current.GetRelationships<ParentOf>();

        // Enumerate to the current index
        var enumerator = children.GetEnumerator();
        var index = 0;

        while (enumerator.MoveNext())
        {
            if (index == _childIndex)
            {
                child = enumerator.Current.Key;
                _childIndex++;
                return true;
            }

            index++;
        }

        child = default;
        return false;
    }

    public bool TryGetNextSibling(out Entity next)
    {
        if (!TryGetParent(out var parent))
        {
            next = default;
            return false;
        }

        ref var siblings = ref parent.GetRelationships<ParentOf>();
        var enumerator = siblings.GetEnumerator();

        var foundCurrent = false;
        while (enumerator.MoveNext())
        {
            if (foundCurrent)
            {
                next = enumerator.Current.Key;
                return true;
            }

            if (enumerator.Current.Key == current)
            {
                foundCurrent = true;
            }
        }

        next = default;
        return false;
    }

    public bool TryGetPreviousSibling(out Entity previous)
    {
        if (!TryGetParent(out var parent))
        {
            previous = default;
            return false;
        }

        ref var siblings = ref parent.GetRelationships<ParentOf>();
        var enumerator = siblings.GetEnumerator();

        Entity prev = default;
        var hasPrevious = false;

        while (enumerator.MoveNext())
        {
            if (enumerator.Current.Key == current)
            {
                if (hasPrevious)
                {
                    previous = prev;
                    return true;
                }

                previous = default;
                return false;
            }

            prev = enumerator.Current.Key;
            hasPrevious = true;
        }

        previous = default;
        return false;
    }
}