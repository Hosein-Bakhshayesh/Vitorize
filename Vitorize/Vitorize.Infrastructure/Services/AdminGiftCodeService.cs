using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.GiftCodes;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Domain.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminGiftCodeService : IAdminGiftCodeService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IEncryptionService _encryptionService;
        private readonly ICurrentUserService _currentUserService;

        public AdminGiftCodeService(
            VitorizeDbContext dbContext,
            IEncryptionService encryptionService,
            ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _encryptionService = encryptionService;
            _currentUserService = currentUserService;
        }

        public async Task<GiftCodeBatchDto> ImportAsync(GiftCodeImportDto request)
        {
            if (request == null)
                throw new BusinessException("اطلاعات وارد شده معتبر نیست.");

            if (request.ProductId == Guid.Empty)
                throw new BusinessException("محصول الزامی است.");

            if (string.IsNullOrWhiteSpace(request.BatchTitle))
                throw new BusinessException("عنوان بچ الزامی است.");

            if (request.Codes == null || !request.Codes.Any())
                throw new BusinessException("حداقل یک کد باید وارد شود.");

            var product = await _dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.ProductId &&
                    !x.IsDeleted &&
                    x.IsActive);

            if (product == null)
                throw new BusinessException("محصول معتبر نیست.");

            if (product.DeliveryType != (byte)DeliveryType.Instant)
                throw new BusinessException("فقط برای محصولات با تحویل آنی امکان وارد کردن کد وجود دارد.");

            if (request.ProductVariantId.HasValue)
            {
                var variantExists = await _dbContext.ProductVariants
                    .AnyAsync(x =>
                        x.Id == request.ProductVariantId.Value &&
                        x.ProductId == request.ProductId &&
                        x.IsActive);

                if (!variantExists)
                    throw new BusinessException("تنوع محصول معتبر نیست.");
            }

            var normalizedCodes = request.Codes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalizedCodes.Any())
                throw new BusinessException("کد معتبری برای ثبت وجود ندارد.");

            if (normalizedCodes.Count != request.Codes.Count(x => !string.IsNullOrWhiteSpace(x)))
                throw new BusinessException("در لیست وارد شده کد تکراری وجود دارد.");

            var fingerprints = normalizedCodes
                .Select(CreateFingerprint)
                .ToList();

            var existingFingerprintExists = await _dbContext.GiftCodes
                .AnyAsync(x =>
                    x.CodeHashFingerprint != null &&
                    fingerprints.Contains(x.CodeHashFingerprint));

            if (existingFingerprintExists)
                throw new BusinessException("یک یا چند کد قبلا در سیستم ثبت شده‌اند.");

            var batch = new GiftCodeBatch
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                BatchTitle = request.BatchTitle.Trim(),
                SourceName = request.SourceName,
                PurchasePrice = request.PurchasePrice,
                Notes = request.Notes,
                ImportedByAdminId = _currentUserService.UserId,
                ImportedAt = DateTime.UtcNow
            };

            foreach (var code in normalizedCodes)
            {
                var giftCode = new GiftCode
                {
                    Id = Guid.NewGuid(),
                    BatchId = batch.Id,
                    ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId,
                    EncryptedCode = _encryptionService.Encrypt(code),
                    MaskedCode = MaskCode(code),
                    Status = (byte)GiftCodeStatus.Available,
                    EncryptionVersion = 1,
                    CodeHashFingerprint = CreateFingerprint(code),
                    CreatedAt = DateTime.UtcNow
                };

                batch.GiftCodes.Add(giftCode);
            }

            await _dbContext.GiftCodeBatches.AddAsync(batch);
            await _dbContext.SaveChangesAsync();

            return await GetBatchByIdAsync(batch.Id);
        }

        public async Task<List<GiftCodeBatchDto>> GetBatchesAsync()
        {
            return await _dbContext.GiftCodeBatches
                .AsNoTracking()
                .OrderByDescending(x => x.ImportedAt)
                .Select(x => new GiftCodeBatchDto
                {
                    Id = x.Id,
                    BatchTitle = x.BatchTitle,
                    SourceName = x.SourceName,
                    TotalCodes = x.GiftCodes.Count,
                    AvailableCodes = x.GiftCodes.Count(c => c.Status == (byte)GiftCodeStatus.Available),
                    ImportedAt = x.ImportedAt
                })
                .ToListAsync();
        }

        public async Task DeleteBatchAsync(Guid batchId)
        {
            var batch = await _dbContext.GiftCodeBatches
                .Include(x => x.GiftCodes)
                .FirstOrDefaultAsync(x => x.Id == batchId);

            if (batch == null)
                throw new NotFoundException("بچ کدها یافت نشد.");

            var hasUsedCode = batch.GiftCodes.Any(x =>
                x.Status != (byte)GiftCodeStatus.Available);

            if (hasUsedCode)
                throw new BusinessException("این بچ دارای کد استفاده شده یا رزرو شده است و قابل حذف نیست.");

            _dbContext.GiftCodes.RemoveRange(batch.GiftCodes);
            _dbContext.GiftCodeBatches.Remove(batch);

            await _dbContext.SaveChangesAsync();
        }

        private async Task<GiftCodeBatchDto> GetBatchByIdAsync(Guid batchId)
        {
            var batch = await _dbContext.GiftCodeBatches
                .AsNoTracking()
                .Where(x => x.Id == batchId)
                .Select(x => new GiftCodeBatchDto
                {
                    Id = x.Id,
                    BatchTitle = x.BatchTitle,
                    SourceName = x.SourceName,
                    TotalCodes = x.GiftCodes.Count,
                    AvailableCodes = x.GiftCodes.Count(c => c.Status == (byte)GiftCodeStatus.Available),
                    ImportedAt = x.ImportedAt
                })
                .FirstOrDefaultAsync();

            if (batch == null)
                throw new NotFoundException("بچ کدها یافت نشد.");

            return batch;
        }

        private static string MaskCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "****";

            var cleaned = code.Trim();

            if (cleaned.Length <= 4)
                return "****";

            var lastPart = cleaned[^4..];

            return $"****-{lastPart}";
        }

        private static string CreateFingerprint(string code)
        {
            var normalized = code.Trim().ToUpperInvariant();
            var bytes = Encoding.UTF8.GetBytes(normalized);
            var hashBytes = SHA256.HashData(bytes);

            return Convert.ToBase64String(hashBytes);
        }
    }
}