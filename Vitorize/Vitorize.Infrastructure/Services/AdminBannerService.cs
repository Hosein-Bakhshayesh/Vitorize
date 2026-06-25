using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Banners;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminBannerService : IAdminBannerService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminBannerService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AdminBannerDto>> GetAllAsync()
        {
            return await _dbContext.Banners
                .AsNoTracking()
                .OrderBy(x => x.Position)
                .ThenBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => new AdminBannerDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    ImagePath = x.ImagePath,
                    MobileImagePath = x.MobileImagePath,
                    LinkUrl = x.LinkUrl,
                    Position = x.Position,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    StartsAt = x.StartsAt,
                    EndsAt = x.EndsAt,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<AdminBannerDto> GetByIdAsync(Guid id)
        {
            var banner = await _dbContext.Banners
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AdminBannerDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    ImagePath = x.ImagePath,
                    MobileImagePath = x.MobileImagePath,
                    LinkUrl = x.LinkUrl,
                    Position = x.Position,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    StartsAt = x.StartsAt,
                    EndsAt = x.EndsAt,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (banner == null)
                throw new NotFoundException("بنر یافت نشد.");

            return banner;
        }

        public async Task<AdminBannerDto> CreateAsync(CreateBannerRequestDto request)
        {
            Normalize(request);
            Validate(request);

            var banner = new Banner
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                ImagePath = request.ImagePath,
                MobileImagePath = request.MobileImagePath,
                LinkUrl = request.LinkUrl,
                Position = request.Position,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Banners.AddAsync(banner);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(banner.Id);
        }

        public async Task<AdminBannerDto> UpdateAsync(Guid id, UpdateBannerRequestDto request)
        {
            Normalize(request);
            Validate(request);

            var banner = await _dbContext.Banners
                .FirstOrDefaultAsync(x => x.Id == id);

            if (banner == null)
                throw new NotFoundException("بنر یافت نشد.");

            banner.Title = request.Title;
            banner.ImagePath = request.ImagePath;
            banner.MobileImagePath = request.MobileImagePath;
            banner.LinkUrl = request.LinkUrl;
            banner.Position = request.Position;
            banner.SortOrder = request.SortOrder;
            banner.IsActive = request.IsActive;
            banner.StartsAt = request.StartsAt;
            banner.EndsAt = request.EndsAt;

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(banner.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var banner = await _dbContext.Banners
                .FirstOrDefaultAsync(x => x.Id == id);

            if (banner == null)
                throw new NotFoundException("بنر یافت نشد.");

            _dbContext.Banners.Remove(banner);
            await _dbContext.SaveChangesAsync();
        }

        private static void Normalize(CreateBannerRequestDto request)
        {
            request.Title = request.Title?.Trim() ?? string.Empty;
            request.ImagePath = request.ImagePath?.Trim() ?? string.Empty;
            request.MobileImagePath = NormalizeNullable(request.MobileImagePath);
            request.LinkUrl = NormalizeNullable(request.LinkUrl);
            request.Position = request.Position?.Trim() ?? string.Empty;

            if (request.SortOrder < 0)
                request.SortOrder = 0;
        }

        private static void Validate(CreateBannerRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new BusinessException("عنوان بنر الزامی است.");

            if (string.IsNullOrWhiteSpace(request.ImagePath))
                throw new BusinessException("تصویر بنر الزامی است.");

            if (string.IsNullOrWhiteSpace(request.Position))
                throw new BusinessException("جایگاه نمایش بنر الزامی است.");

            if (request.StartsAt.HasValue && request.EndsAt.HasValue && request.StartsAt.Value > request.EndsAt.Value)
                throw new BusinessException("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
