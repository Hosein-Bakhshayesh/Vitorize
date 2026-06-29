namespace Vitorize.Web.Services.UI
{
    public enum ToastLevel
    {
        Success,
        Error,
        Warning,
        Info
    }

    public class ToastMessage
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public ToastLevel Level { get; set; } = ToastLevel.Info;
    }

    /// <summary>
    /// سرویس نمایش پیام‌های لحظه‌ای موفقیت/خطا برای کاربر ادمین.
    /// </summary>
    public class ToastService
    {
        public event Action? OnChange;

        public List<ToastMessage> Messages { get; } = new();

        public void Success(string text) => Show(text, ToastLevel.Success);
        public void Error(string text) => Show(text, ToastLevel.Error);
        public void Warning(string text) => Show(text, ToastLevel.Warning);
        public void Info(string text) => Show(text, ToastLevel.Info);

        public void Show(string text, ToastLevel level = ToastLevel.Info)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var message = new ToastMessage { Text = text, Level = level };
            Messages.Add(message);
            OnChange?.Invoke();

            _ = AutoRemoveAsync(message);
        }

        public void Remove(ToastMessage message)
        {
            if (Messages.Remove(message))
                OnChange?.Invoke();
        }

        private async Task AutoRemoveAsync(ToastMessage message)
        {
            await Task.Delay(5000);
            Remove(message);
        }
    }
}
