using System.Text.Json;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Icons;

namespace Vitorize.Application.Common;

public static class LucideIconRules
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (LucideIconCatalog.TryNormalizeKey(value, out var normalized)) return normalized;
        throw new BusinessException("آیکون انتخاب‌شده در کاتالوگ رسمی Lucide وجود ندارد.");
    }

    public static string NormalizeRequired(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null) throw new BusinessException("انتخاب آیکون الزامی است.");
        return normalized;
    }

    public static string NormalizeConfigurableBlocksJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "[]";
        List<ConfigurableIconBlock>? blocks;
        try
        {
            blocks = JsonSerializer.Deserialize<List<ConfigurableIconBlock>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw new BusinessException("ساختار تنظیمات کارت‌های آیکون‌دار معتبر نیست.");
        }

        if (blocks is null || blocks.Count > 24)
            throw new BusinessException("حداکثر ۲۴ کارت آیکون‌دار قابل ذخیره است.");

        foreach (var block in blocks)
        {
            block.Icon = NormalizeRequired(block.Icon);
            block.Title = block.Title?.Trim() ?? string.Empty;
            block.Text = block.Text?.Trim() ?? string.Empty;
            if (block.Title.Length is < 1 or > 120 || block.Text.Length > 500)
                throw new BusinessException("عنوان یا توضیح کارت آیکون‌دار معتبر نیست.");
        }

        return JsonSerializer.Serialize(blocks, JsonOptions);
    }

    private sealed class ConfigurableIconBlock
    {
        public string? Icon { get; set; }
        public string? Title { get; set; }
        public string? Text { get; set; }
    }
}
