using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Typography;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin;

[ApiController, Authorize(Policy = "AdminOnly"), Route("api/admin/fonts")]
public sealed class AdminFontsController : ControllerBase
{
    private static readonly Regex SafeFamily = new("^[\\p{L}\\p{N} _-]{2,60}$", RegexOptions.Compiled);
    private readonly VitorizeDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ISettingService _settings;

    public AdminFontsController(VitorizeDbContext db, IWebHostEnvironment environment, ISettingService settings)
        => (_db, _environment, _settings) = (db, environment, settings);

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<FontAssetDto>>>> GetAll() => Ok(ApiResult<List<FontAssetDto>>.Success(
        await _db.FontAssets.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.FamilyName)
            .Select(x => ToDto(x)).ToListAsync()));

    [HttpPost, RequestSizeLimit(20 * 1024 * 1024 + 64 * 1024)]
    public async Task<ActionResult<ApiResult<FontAssetDto>>> Upload([FromForm] IFormFile file, [FromForm] string familyName)
    {
        familyName = (familyName ?? string.Empty).Trim();
        if (!SafeFamily.IsMatch(familyName)) throw new BusinessException("نام خانوادگی فونت باید ۲ تا ۶۰ نویسه و بدون کاراکتر کنترلی باشد.");
        if (await _db.FontAssets.AnyAsync(x => x.FamilyName == familyName)) throw new BusinessException("این نام فونت قبلاً ثبت شده است.");
        var configuredMb = await _settings.GetValueAsync<int>("Typography.MaxUploadMb");
        var maxBytes = Math.Clamp(configuredMb <= 0 ? 5 : configuredMb, 1, 20) * 1024L * 1024L;
        var header = new byte[4];
        await using (var input = file.OpenReadStream())
            if (await input.ReadAsync(header) < 4) throw new BusinessException("فایل فونت ناقص است.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var format = FontFileValidator.Validate(extension, file.ContentType, file.Length, header, maxBytes);
        var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(root, "uploads", "fonts");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, fileName);
        await using (var output = System.IO.File.Create(fullPath)) await file.CopyToAsync(output);

        var asset = new FontAsset
        {
            Id = Guid.NewGuid(), FamilyName = familyName, FilePath = $"/uploads/fonts/{fileName}",
            FileFormat = format, MimeType = file.ContentType, SizeBytes = file.Length,
            Scope = (byte)FontApplicationScope.EntireApplication, IsBuiltIn = false, IsActive = false,
            CreatedByUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null,
            CreatedAt = DateTime.UtcNow
        };
        _db.FontAssets.Add(asset);
        await _db.SaveChangesAsync();
        return Ok(ApiResult<FontAssetDto>.Success(ToDto(asset), "فونت با موفقیت و با نام امن سرور ذخیره شد."));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ApiResult<FontAssetDto>>> Activate(Guid id, ActivateFontRequestDto request)
    {
        if (!Enum.IsDefined(typeof(FontApplicationScope), request.Scope)) throw new BusinessException("محدوده اعمال فونت معتبر نیست.");
        var selected = await _db.FontAssets.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("فونت یافت نشد.");
        await using var transaction = await _db.Database.BeginTransactionAsync();
        foreach (var font in await _db.FontAssets.Where(x => x.IsActive).ToListAsync()) font.IsActive = false;
        selected.IsActive = true; selected.Scope = request.Scope; selected.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await UpsertTypography("Typography.FontFamily", selected.FamilyName, "string", "نام فونت فعال");
        await UpsertTypography("Typography.FontPath", selected.FilePath ?? string.Empty, "string", "مسیر فونت فعال");
        await UpsertTypography("Typography.FontFormat", selected.FileFormat, "string", "فرمت فونت فعال");
        await UpsertTypography("Typography.Scope", request.Scope.ToString(), "int", "محدوده اعمال: ۱ فروشگاه، ۲ مدیریت، ۳ کل برنامه");
        await UpsertTypography("Typography.Version", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), "string", "نسخه کش فونت");
        await transaction.CommitAsync();
        return Ok(ApiResult<FontAssetDto>.Success(ToDto(selected), "فونت فعال شد."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResult>> Delete(Guid id)
    {
        var asset = await _db.FontAssets.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("فونت یافت نشد.");
        if (asset.IsBuiltIn || asset.IsActive) throw new BusinessException("فونت داخلی یا فعال قابل حذف نیست.");
        _db.FontAssets.Remove(asset); await _db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(asset.FilePath))
        {
            var root = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
            var fontsRoot = Path.GetFullPath(Path.Combine(root, "uploads", "fonts")) + Path.DirectorySeparatorChar;
            var file = Path.GetFullPath(Path.Combine(root, asset.FilePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));
            if (file.StartsWith(fontsRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(file)) System.IO.File.Delete(file);
        }
        return Ok(ApiResult.Success("فونت حذف شد."));
    }

    private Task UpsertTypography(string key, string value, string type, string description) =>
        _settings.UpsertAsync(new UpdateSettingDto { Key = key, Value = value, GroupName = "Typography", ValueType = type, Description = description });

    private static FontAssetDto ToDto(FontAsset x) => new()
    {
        Id = x.Id, FamilyName = x.FamilyName, FilePath = x.FilePath, FileFormat = x.FileFormat,
        SizeBytes = x.SizeBytes, IsBuiltIn = x.IsBuiltIn, IsActive = x.IsActive, Scope = x.Scope, CreatedAt = x.CreatedAt
    };
}
