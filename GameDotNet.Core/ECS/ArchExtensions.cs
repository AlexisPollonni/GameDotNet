using Arch.Core;
using Arch.Core.Extensions;

namespace GameDotNet.Core.ECS;

public static class ArchExtensions
{
    public static Entity GetFirstEntity(this World world, in QueryDescription description)
    {
        return GetFirstEntityOrDefault(world, description) ??
               throw new InvalidOperationException("Entity not found in world");
    }

    public static Entity? GetFirstEntityOrDefault(this World world, in QueryDescription description)
    {
        var query = world.Query(description);

        var counter = 0;
        foreach (var archetype in query.GetArchetypeIterator())
            counter += archetype.Entities;

        Entity? entity = null;

        if (counter > 0)
        {
            var e = query.GetEnumerator();
            if (!e.MoveNext()) return null;

            entity = e.Current.Entity(0);
        }

        if (entity?.IsAlive() ?? false)
            return entity;

        return null;
    }
}