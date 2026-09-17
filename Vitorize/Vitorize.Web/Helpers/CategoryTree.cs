using Vitorize.Web.Models.Store;

namespace Vitorize.Web.Helpers
{
    /// <summary>
    /// Shapes the flat category lookup (<c>products/categories</c>, the only storefront endpoint that
    /// carries <see cref="StoreLookupModel.ParentId"/>) into the parent/child relations every category
    /// surface needs. The homepage board and both header menus render the same hierarchy, so the rules
    /// for walking it live here once rather than being re-derived at each call site.
    /// </summary>
    public static class CategoryTree
    {
        public static List<StoreLookupModel> Roots(IReadOnlyList<StoreLookupModel> all) =>
            all.Where(c => c.ParentId is null).ToList();

        /// <summary>
        /// Roots the header menus should list. The lookup itself stays complete, because the shop
        /// filters and the full category page must keep showing everything; only the menus narrow.
        /// </summary>
        public static List<StoreLookupModel> MenuRoots(IReadOnlyList<StoreLookupModel> all) =>
            all.Where(c => c.ParentId is null && c.ShowInMenu).ToList();

        public static List<StoreLookupModel> ChildrenOf(IReadOnlyList<StoreLookupModel> all, Guid parentId) =>
            all.Where(c => c.ParentId == parentId).ToList();

        /// <summary>
        /// Every category below <paramref name="parentId"/>, flattened breadth-first. Surfaces draw a
        /// fixed number of tiers; flattening keeps a deeper tree reachable instead of dropping its
        /// leaves silently. A mis-configured parent cycle is broken rather than allowed to hang.
        /// </summary>
        public static List<StoreLookupModel> DescendantsOf(IReadOnlyList<StoreLookupModel> all, Guid parentId)
        {
            var result = new List<StoreLookupModel>();
            var seen = new HashSet<Guid>();
            var queue = new Queue<Guid>();
            queue.Enqueue(parentId);

            while (queue.Count > 0)
            {
                foreach (var child in ChildrenOf(all, queue.Dequeue()))
                {
                    if (!seen.Add(child.Id)) continue;
                    result.Add(child);
                    queue.Enqueue(child.Id);
                }
            }

            return result;
        }
    }
}
