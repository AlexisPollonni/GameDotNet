using Arch.Core;
using Arch.Relationships;
using ZLinq;

namespace GameDotNet.Core.Tooling.Extensions;

public static class ArchEntityMutationExtensions
{
    extension(Entity thisEntity)
    {
        #region Hierarchy Mutation - Adding Children
        
        /// <summary>
        /// Adds a child to this entity. This entity becomes the parent.
        /// </summary>
        /// <param name="child">The entity to add as a child</param>
        public void AddChild(Entity child)
        {
            // Remove from existing parent first
            child.Parent?.RemoveChild(child);
            
            thisEntity.AddRelationship<ParentOf>(child);
            child.AddRelationship<ChildOf>(thisEntity);
        }

        /// <summary>
        /// Adds multiple children to this entity.
        /// </summary>
        public void AddChildren(params ReadOnlySpan<Entity> children)
        {
            foreach (var child in children)
            {
                thisEntity.AddChild(child);
            }
        }

        /// <summary>
        /// Adds multiple children to this entity.
        /// </summary>
        public void AddChildren(IEnumerable<Entity> children)
        {
            foreach (var child in children)
            {
                thisEntity.AddChild(child);
            }
        }

        #endregion

        #region Hierarchy Mutation - Removing Children

        /// <summary>
        /// Removes a child from this entity, making it a root entity.
        /// </summary>
        /// <param name="child">The child entity to remove</param>
        public void RemoveChild(Entity child)
        {
            thisEntity.RemoveRelationship<ParentOf>(child);
            child.RemoveRelationship<ChildOf>(thisEntity);
        }

        /// <summary>
        /// Removes all children from this entity, making them root entities.
        /// </summary>
        public void RemoveAllChildren()
        {
            foreach (var child in thisEntity.Children())
            {
                thisEntity.RemoveChild(child);
            }
        }

        /// <summary>
        /// Removes multiple children from this entity.
        /// </summary>
        public void RemoveChildren(params ReadOnlySpan<Entity> children)
        {
            foreach (var child in children)
            {
                thisEntity.RemoveChild(child);
            }
        }

        #endregion

        #region Hierarchy Mutation - Setting Parent

        /// <summary>
        /// Sets the parent of this entity, removing any existing parent relationship.
        /// Pass null to detach from parent (make this a root entity).
        /// </summary>
        /// <param name="newParent">The new parent entity, or null to detach</param>
        public void SetParent(Entity? newParent)
        {
            // Remove from current parent
            thisEntity.Parent?.RemoveChild(thisEntity);

            // Add to new parent
            newParent?.AddChild(thisEntity);
        }

        /// <summary>
        /// Detaches this entity from its parent, making it a root entity.
        /// Same as SetParent(null).
        /// </summary>
        public void DetachFromParent()
        {
            thisEntity.SetParent(null);
        }

        /// <summary>
        /// Reparents this entity to a new parent while maintaining its position in the sibling list.
        /// If the new parent has fewer children than the current index, it will be added at the end.
        /// </summary>
        public void ReparentTo(Entity newParent)
        {
            var currentIndex = thisEntity.SiblingIndex;
            thisEntity.SetParent(newParent);
            
            if (currentIndex.HasValue)
            {
                thisEntity.SetSiblingIndex(currentIndex.Value);
            }
        }

        #endregion

        #region Sibling Manipulation

        /// <summary>
        /// Moves this entity to be the first child of its parent.
        /// Does nothing if the entity has no parent.
        /// </summary>
        public void MoveToFirstSibling()
        {
            if (!thisEntity.HasParent) return;
            
            var parent = thisEntity.Parent!.Value;
            parent.RemoveChild(thisEntity);
            
            // Re-add as first (we need to add before all others)
            var siblings = parent.Children().ToArray();
            
            thisEntity.AddRelationship<ChildOf>(parent);
            parent.AddRelationship<ParentOf>(thisEntity);
            
            // Re-add all siblings after this one
            foreach (var sibling in siblings)
            {
                parent.RemoveRelationship<ParentOf>(sibling);
                parent.AddRelationship<ParentOf>(sibling);
            }
        }

        /// <summary>
        /// Moves this entity to be the last child of its parent.
        /// Does nothing if the entity has no parent.
        /// </summary>
        public void MoveToLastSibling()
        {
            if (!thisEntity.HasParent) return;
            
            var parent = thisEntity.Parent!.Value;
            parent.RemoveChild(thisEntity);
            parent.AddChild(thisEntity);
        }

        /// <summary>
        /// Sets the sibling index (position among siblings) of this entity.
        /// 0 = first child, 1 = second child, etc.
        /// If index is out of range, clamps to valid range.
        /// </summary>
        public void SetSiblingIndex(int index)
        {
            if (!thisEntity.HasParent) return;
            
            var parent = thisEntity.Parent!.Value;
            var siblings = parent.Children().ToArray();
            var currentIndex = Array.IndexOf(siblings, thisEntity);
            
            if (currentIndex < 0 || currentIndex == index) return;
            
            // Clamp index
            index = Math.Clamp(index, 0, siblings.Length - 1);
            
            // Remove all children and re-add in new order
            foreach (var sibling in siblings)
            {
                parent.RemoveRelationship<ParentOf>(sibling);
            }
            
            var newOrder = siblings.Where(s => s != thisEntity).ToList();
            newOrder.Insert(index, thisEntity);
            
            foreach (var sibling in newOrder)
            {
                parent.AddRelationship<ParentOf>(sibling);
                sibling.AddRelationship<ChildOf>(parent);
            }
        }

        /// <summary>
        /// Moves this entity up one position in the sibling list (decreases sibling index by 1).
        /// Does nothing if already first child or has no parent.
        /// </summary>
        public void MoveSiblingUp()
        {
            var index = thisEntity.SiblingIndex;
            if (index > 0)
            {
                thisEntity.SetSiblingIndex(index.Value - 1);
            }
        }

        /// <summary>
        /// Moves this entity down one position in the sibling list (increases sibling index by 1).
        /// Does nothing if already last child or has no parent.
        /// </summary>
        public void MoveSiblingDown()
        {
            var index = thisEntity.SiblingIndex;
            if (index.HasValue && !thisEntity.IsLastChild)
            {
                thisEntity.SetSiblingIndex(index.Value + 1);
            }
        }

        /// <summary>
        /// Swaps the position of this entity with another sibling entity.
        /// Does nothing if they are not siblings.
        /// </summary>
        public void SwapSiblingWith(Entity otherSibling)
        {
            if (!thisEntity.IsSiblingOf(otherSibling)) return;
            
            var thisIndex = thisEntity.SiblingIndex;
            var otherIndex = otherSibling.SiblingIndex;
            
            if (!thisIndex.HasValue || !otherIndex.HasValue) return;
            
            thisEntity.SetSiblingIndex(otherIndex.Value);
            otherSibling.SetSiblingIndex(thisIndex.Value);
        }

        #endregion

        #region Hierarchy Destruction

        public void Destroy()
        {
            thisEntity.World.Destroy(thisEntity);
        }
        
        /// <summary>
        /// Destroys this entity and all its descendants recursively (bottom-up).
        /// Children are destroyed before their parents.
        /// </summary>
        public void DestroyWithChildren()
        {
            // Destroy descendants first (bottom-up) to avoid dangling references
            foreach (var descendant in thisEntity.Descendants().Reverse())
            {
                descendant.Destroy();
            }
            thisEntity.Destroy();
        }

        /// <summary>
        /// Destroys only this entity. Its children are reparented to this entity's parent.
        /// If this entity has no parent, its children become root entities.
        /// </summary>
        public void DestroyAndPromoteChildren()
        {
            var parent = thisEntity.Parent;
            using var children = thisEntity.Children().ToArrayPool();
            
            // Reparent all children
            foreach (var child in children.Array)
            {
                if (parent.HasValue)
                {
                    child.SetParent(parent.Value);
                }
                else
                {
                    child.DetachFromParent();
                }
            }
            
            thisEntity.Destroy();
        }

        /// <summary>
        /// Destroys only this entity. Its children become root entities (orphaned).
        /// </summary>
        public void DestroyAndOrphanChildren()
        {
            using var children = thisEntity.Children().ToArrayPool();
            
            foreach (var child in children.Array)
            {
                child.DetachFromParent();
            }
            
            thisEntity.Destroy();
        }

        #endregion

        #region Hierarchy Manipulation

        /// <summary>
        /// Transfers all children from this entity to another entity.
        /// </summary>
        public void TransferChildrenTo(Entity newParent)
        {
            var children = thisEntity.Children().ToArray();
            foreach (var child in children)
            {
                child.SetParent(newParent);
            }
        }

        /// <summary>
        /// Creates a duplicate hierarchy from this entity (deep copy structure).
        /// Note: This only duplicates the hierarchy structure, not the component data.
        /// Components must be copied separately if needed.
        /// </summary>
        public Entity DuplicateHierarchy(World world)
        {
            var clone = world.Create();
            
            // Duplicate all descendants
            foreach (var child in thisEntity.Children())
            {
                var childClone = child.DuplicateHierarchy(world);
                clone.AddChild(childClone);
            }
            
            return clone;
        }

        /// <summary>
        /// Sorts children of this entity using the specified comparison.
        /// </summary>
        public void SortChildren(Comparison<Entity> comparison)
        {
            var children = thisEntity.Children().ToArray();
            Array.Sort(children, comparison);
            
            // Remove all and re-add in sorted order
            foreach (var child in children)
            {
                thisEntity.RemoveRelationship<ParentOf>(child);
            }
            
            foreach (var child in children)
            {
                thisEntity.AddRelationship<ParentOf>(child);
                child.AddRelationship<ChildOf>(thisEntity);
            }
        }

        /// <summary>
        /// Sorts children of this entity using the specified comparer.
        /// </summary>
        public void SortChildren(IComparer<Entity> comparer)
        {
            thisEntity.SortChildren(comparer.Compare);
        }

        #endregion
    }
}