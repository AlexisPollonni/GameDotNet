using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.ECS;
using ZLinq;
using ZLinq.Linq;
using ZLinq.Traversables;

namespace GameDotNet.Core.Tooling.Extensions;


/// <summary>
/// Provides a simple component to label entities.
/// </summary>
/// <param name="Name"></param>
public readonly record struct Label(string Name);

/// <summary>
/// For entities that can be identified uniquely. Useful when persisting entities.
/// </summary>
/// <param name="Id"></param>
public readonly record struct Identifiable(Guid Id);

/// <summary>
/// Tag relationship component indicating that an entity is a parent of another entity.
/// </summary>
internal readonly record struct ParentOf;

/// <summary>
/// Tag relationship component indicating that an entity is a child of another entity.
/// </summary>
internal readonly record struct ChildOf;



public static class ArchEntityCommonExtensions
{
    extension(Entity thisEntity)
    {
        /// <summary>
        /// Creates a traverser for navigating the entity hierarchy.
        /// </summary>
        internal EntityTraverser Traverser => new(thisEntity);

        /// <summary>
        /// Gets the world this entity belongs to.
        /// </summary>
        public World World => World.Worlds[thisEntity.WorldId];

        /// <summary>
        /// Gets the parent entity, if one exists.
        /// </summary>
        public Entity? Parent => thisEntity.Traverser.Ancestors().FirstOrDefault();

        /// <summary>
        /// Checks if this entity has a parent.
        /// </summary>
        public bool HasParent => thisEntity.Parent is not null;

        /// <summary>
        /// Checks if this entity has any children.
        /// </summary>
        public bool HasChildren => thisEntity.Children().FirstOrDefault(Entity.Null) != Entity.Null;

        /// <summary>
        /// Gets the number of direct children.
        /// </summary>
        public int ChildCount
        {
            get
            {
                thisEntity.Traverser.TryGetChildCount(out var count);
                return count;
            }
        }

        /// <summary>
        /// Gets the root entity of the hierarchy (topmost ancestor with no parent).
        /// </summary>
        public Entity Root => thisEntity.Ancestors().LastOrDefault(thisEntity);

        /// <summary>
        /// Gets the depth of this entity in the hierarchy (0 for root, 1 for direct child of root, etc.).
        /// </summary>
        public int Depth => thisEntity.Ancestors().Count();

        /// <summary>
        /// Retrieves the label of this entity, if it has one.
        /// </summary>
        public string? Label => thisEntity.TryGet(out Label label) ? label.Name : null;
        
        /// <summary>
        /// Retrieves the ID of this entity, if it has one.
        /// </summary>
        public Guid? Id => thisEntity.TryGet(out Identifiable identifiable) ? identifiable.Id : null;

        /// <summary>
        /// Gets the sibling index of this entity (position among siblings, 0-based).
        /// Returns null if the entity has no parent.
        /// </summary>
        public int? SiblingIndex
        {
            get
            {
                var parent = thisEntity.Parent;
                if (!parent.HasValue) return null;
                
                int index = 0;
                foreach (var sibling in parent.Value.Children())
                {
                    if (sibling == thisEntity) return index;
                    index++;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the total number of siblings (including this entity).
        /// Returns 0 if the entity has no parent.
        /// </summary>
        public int SiblingCount => thisEntity.Parent?.ChildCount ?? 0;

        /// <summary>
        /// Checks if this entity is the first child of its parent.
        /// </summary>
        public bool IsFirstChild => thisEntity.SiblingIndex == 0;

        /// <summary>
        /// Checks if this entity is the last child of its parent.
        /// </summary>
        public bool IsLastChild
        {
            get
            {
                var index = thisEntity.SiblingIndex;
                return index.HasValue && index.Value == thisEntity.SiblingCount - 1;
            }
        }

        /// <summary>
        /// Checks if this entity is a root entity (has no parent).
        /// </summary>
        public bool IsRoot => !thisEntity.HasParent;

        /// <summary>
        /// Checks if this entity is a leaf entity (has no children).
        /// </summary>
        public bool IsLeaf => !thisEntity.HasChildren;

        /// <summary>
        /// Gets the total number of descendants (children, grandchildren, etc.).
        /// </summary>
        public int DescendantCount => thisEntity.Descendants().Count();

        /// <summary>
        /// Checks if this entity is an ancestor of the specified entity.
        /// </summary>
        public bool IsAncestorOf(Entity descendant) =>
            descendant.Ancestors().Contains(thisEntity);

        /// <summary>
        /// Checks if this entity is a descendant of the specified entity.
        /// </summary>
        public bool IsDescendantOf(Entity ancestor) =>
            thisEntity.Ancestors().Contains(ancestor);

        /// <summary>
        /// Checks if this entity is a sibling of the specified entity (shares the same parent).
        /// </summary>
        public bool IsSiblingOf(Entity other) =>
            thisEntity.BeforeSelfAndSelf().Concat(thisEntity.AfterSelf()).Contains(other);

        /// <summary>
        /// Finds the first child entity with the specified label.
        /// </summary>
        public Entity? FindChildByLabel(string label) =>
            thisEntity.Children().FirstOrDefault(entity => entity.Label == label);

        /// <summary>
        /// Finds the first descendant entity with the specified label (depth-first search).
        /// </summary>
        public Entity? FindDescendantByLabel(string label) =>
            thisEntity.Descendants().FirstOrDefault(entity => entity.Label == label);

        /// <summary>
        /// Finds all descendants with the specified label.
        /// </summary>
        public ValueEnumerable<Where<Descendants<EntityTraverser, Entity>, Entity>, Entity> FindDescendantsByLabel(
            string label) =>
            thisEntity.Descendants().Where(entity => entity.Label == label);
    }
}