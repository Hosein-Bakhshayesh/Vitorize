namespace Vitorize.Web.Services.UI
{
    /// <summary>
    /// نگه‌دارنده‌ی تعداد اقلام سبد خرید برای نمایش نشان (badge) در هدر فروشگاه.
    /// </summary>
    public class CartState
    {
        public int Count { get; private set; }

        public event Action? OnChange;

        public void Set(int count)
        {
            Count = count < 0 ? 0 : count;
            OnChange?.Invoke();
        }

        public void Clear() => Set(0);
    }
}
