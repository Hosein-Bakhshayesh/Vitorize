namespace Vitorize.Infrastructure.Services
{
    /// <summary>One flat category row: everything the tree walk needs, and nothing else.</summary>
    internal readonly record struct CategoryNode(Guid Id, Guid? ParentId);

    /// <summary>
    /// Walks the category tree in memory. Both the storefront listing and the admin category list
    /// need the same question answered — "what sits at and below this category?" — so the traversal
    /// lives here once rather than being re-derived, and with it the guard against a parent cycle.
    /// <para>
    /// Categories are few in any catalogue, so the flat rows are cheap to read and fold; the
    /// alternative, a recursive CTE, would tie both callers to SQL Server while the test suite runs
    /// on the in-memory provider.
    /// </para>
    /// </summary>
    internal static class CategoryHierarchy
    {
        public static Dictionary<Guid, List<Guid>> ChildrenByParent(IEnumerable<CategoryNode> nodes)
        {
            var map = new Dictionary<Guid, List<Guid>>();

            foreach (var node in nodes)
            {
                if (node.ParentId is not { } parentId)
                    continue;

                if (!map.TryGetValue(parentId, out var children))
                    map[parentId] = children = new List<Guid>();

                children.Add(node.Id);
            }

            return map;
        }

        /// <summary>
        /// The ids at and below <paramref name="rootId"/>, the root included. An unknown id yields
        /// just itself, so a caller filtering on a missing category behaves as it always did.
        /// </summary>
        public static HashSet<Guid> Branch(Dictionary<Guid, List<Guid>> childrenByParent, Guid rootId)
        {
            var branch = new HashSet<Guid> { rootId };
            var pending = new Stack<Guid>();
            pending.Push(rootId);

            while (pending.Count > 0)
            {
                if (!childrenByParent.TryGetValue(pending.Pop(), out var children))
                    continue;

                foreach (var child in children)
                {
                    // Writes reject a cycle, so this only guards rows that predate that validation.
                    if (branch.Add(child))
                        pending.Push(child);
                }
            }

            return branch;
        }
    }
}
