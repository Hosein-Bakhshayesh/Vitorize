namespace Vitorize.Domain.Entities;

/// <summary>
/// شمارندهٔ سراسریِ شمارهٔ قابل‌نمایش سفارش‌های پرداخت‌شده.
/// تنها یک ردیف با شناسهٔ ۱ دارد و هرگز به تنظیمات قابل‌ویرایش مدیر متصل نیست.
/// </summary>
public partial class OrderNumberCounter
{
    public byte Id { get; set; }
    public long NextNumber { get; set; }
}
