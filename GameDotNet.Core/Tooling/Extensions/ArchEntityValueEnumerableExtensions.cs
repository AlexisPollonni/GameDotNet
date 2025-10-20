using Arch.Core;
using GameDotNet.Core.ECS;
using ZLinq;
using ZLinq.Traversables;

namespace GameDotNet.Core.Tooling.Extensions;

public static class ArchEntityValueEnumerableExtensions
{
    extension(Entity thisEntity)
    {
        // Type 
        public ValueEnumerable<Children<EntityTraverser, Entity>, Entity> Children() =>
            thisEntity.Traverser.Children();

        public ValueEnumerable<Children<EntityTraverser, Entity>, Entity> ChildrenAndSelf() =>
            thisEntity.Traverser.ChildrenAndSelf();

        public ValueEnumerable<Descendants<EntityTraverser, Entity>, Entity> Descendants() =>
            thisEntity.Traverser.Descendants();

        public ValueEnumerable<Descendants<EntityTraverser, Entity>, Entity> DescendantsAndSelf() =>
            thisEntity.Traverser.DescendantsAndSelf();

        public ValueEnumerable<Ancestors<EntityTraverser, Entity>, Entity> Ancestors() =>
            thisEntity.Traverser.Ancestors();

        public ValueEnumerable<Ancestors<EntityTraverser, Entity>, Entity> AncestorsAndSelf() =>
            thisEntity.Traverser.AncestorsAndSelf();

        public ValueEnumerable<BeforeSelf<EntityTraverser, Entity>, Entity> BeforeSelf() =>
            thisEntity.Traverser.BeforeSelf();

        public ValueEnumerable<BeforeSelf<EntityTraverser, Entity>, Entity> BeforeSelfAndSelf() =>
            thisEntity.Traverser.BeforeSelfAndSelf();

        public ValueEnumerable<AfterSelf<EntityTraverser, Entity>, Entity> AfterSelf() =>
            thisEntity.Traverser.AfterSelf();

        public ValueEnumerable<AfterSelf<EntityTraverser, Entity>, Entity> AfterSelfAndSelf() =>
            thisEntity.Traverser.AfterSelfAndSelf();
    }

    extension(EntityTraverser traverser)
    {
        public ValueEnumerable<Children<EntityTraverser, Entity>, Entity> Children() =>
            traverser.Children<EntityTraverser, Entity>();

        public ValueEnumerable<Children<EntityTraverser, Entity>, Entity> ChildrenAndSelf() =>
            traverser.ChildrenAndSelf<EntityTraverser, Entity>();

        public ValueEnumerable<Descendants<EntityTraverser, Entity>, Entity> Descendants() =>
            traverser.Descendants<EntityTraverser, Entity>();

        public ValueEnumerable<Descendants<EntityTraverser, Entity>, Entity> DescendantsAndSelf() =>
            traverser.DescendantsAndSelf<EntityTraverser, Entity>();

        public ValueEnumerable<Ancestors<EntityTraverser, Entity>, Entity> Ancestors() =>
            traverser.Ancestors<EntityTraverser, Entity>();

        public ValueEnumerable<Ancestors<EntityTraverser, Entity>, Entity> AncestorsAndSelf() =>
            traverser.AncestorsAndSelf<EntityTraverser, Entity>();

        public ValueEnumerable<BeforeSelf<EntityTraverser, Entity>, Entity> BeforeSelf() =>
            traverser.BeforeSelf<EntityTraverser, Entity>();

        public ValueEnumerable<BeforeSelf<EntityTraverser, Entity>, Entity> BeforeSelfAndSelf() =>
            traverser.BeforeSelfAndSelf<EntityTraverser, Entity>();

        public ValueEnumerable<AfterSelf<EntityTraverser, Entity>, Entity> AfterSelf() =>
            traverser.AfterSelf<EntityTraverser, Entity>();

        public ValueEnumerable<AfterSelf<EntityTraverser, Entity>, Entity> AfterSelfAndSelf() =>
            traverser.AfterSelfAndSelf<EntityTraverser, Entity>();
    }
}