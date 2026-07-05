namespace Vitorize.Web.Services.UI
{
    /// <summary>
    /// نگه‌دارنده‌ی شناسه محصولات علاقه‌مندی کاربر برای نمایش قلب‌ها و نشان هدر فروشگاه.
    /// </summary>
    public class WishlistState
    {
        private readonly HashSet<Guid> _productIds = new();

        public int Count => _productIds.Count;

        public event Action? OnChange;

        public bool Contains(Guid productId) => _productIds.Contains(productId);

        public void SetIds(IEnumerable<Guid> productIds)
        {
            _productIds.Clear();

            foreach (var id in productIds)
                _productIds.Add(id);

            OnChange?.Invoke();
        }

        public void Add(Guid productId)
        {
            if (_productIds.Add(productId))
                OnChange?.Invoke();
        }

        public void Remove(Guid productId)
        {
            if (_productIds.Remove(productId))
                OnChange?.Invoke();
        }

        public void Clear()
        {
            _productIds.Clear();
            OnChange?.Invoke();
        }
    }
}
