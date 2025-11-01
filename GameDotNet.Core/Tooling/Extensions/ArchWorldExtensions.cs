using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using CommunityToolkit.HighPerformance.Buffers;
using ZLinq;

namespace GameDotNet.Core.Tooling.Extensions;

public static class ArchWorldExtensions
{
    extension(World world)
    {
        public Entity GetFirstEntity(in QueryDescription description)
        {
            return GetFirstEntityOrDefault(world, description) ??
                   throw new InvalidOperationException("Entity not found in world");
        }

        public Entity? GetFirstEntityOrDefault(in QueryDescription description)
        {
            var query = world.Query(description);

            var counter = 0;
            foreach (var archetype in query.GetArchetypeIterator()) counter += archetype.EntityCount;

            Entity? entity = null;

            if (counter > 0)
            {
                var e = query.GetEnumerator();
                if (!e.MoveNext()) return null;

                entity = e.Current.Entity(0);
            }

            if (entity?.IsAlive() ?? false) return entity;

            return null;
        }

        public SpanOwner<Entity> GetEntitiesPooled(QueryDescription description)
        {
            var count = world.CountEntities(in description);

            var entities = SpanOwner<Entity>.Allocate(count);

            world.GetEntities(in description, entities.Span);

            return entities;
        }

        public ValueEnumerable<FromQueryDescription, Entity> QueryEnumerable(QueryDescription description) =>
            new(new(description, world));
    }

    extension(QueryDescription queryDescription)
    {
        public ValueEnumerable<FromQueryDescription, Entity> AsValueEnumerable(World world)
        {
            return new(new(queryDescription, world));
        }
    }

    public ref struct FromQueryDescription(QueryDescription description, World world) : IValueEnumerator<Entity>
    {
        private QueryChunkEnumerator _chunkQuery = world.Query(description).GetChunkIterator().GetEnumerator();
        private EntityEnumerator _entityEnumerator;

        public void Dispose()
        {
            //noop
        }

        public bool TryGetNext(out Entity current)
        {
            while (!_entityEnumerator.MoveNext())
            {
                if (!_chunkQuery.MoveNext())
                {
                    Unsafe.SkipInit(out current);
                    return false;
                }
                
                _entityEnumerator = _chunkQuery.Current.GetEnumerator();
            }

            current = _chunkQuery.Current.Entity(_entityEnumerator.Current);
            return true;
        }

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = world.CountEntities(description);
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<Entity> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Entity> destination, Index offset) =>
            false;
    }
}