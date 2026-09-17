using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.HomeSlides;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminHomeSlideService : IAdminHomeSlideService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminHomeSlideService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AdminHomeSlideDto>> GetAllAsync()
        {
            return await _dbContext.HomeSlides
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => Project(x))
                .ToListAsync();
        }

        public async Task<AdminHomeSlideDto> GetByIdAsync(Guid id)
        {
            var slide = await _dbContext.HomeSlides
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => Project(x))
                .FirstOrDefaultAsync();

            if (slide is null)
                throw new NotFoundException("اسلاید یافت نشد.");

            return slide;
        }

        public async Task<AdminHomeSlideDto> CreateAsync(CreateHomeSlideRequestDto request)
        {
            Normalize(request);
            Validate(request);

            var slide = new HomeSlide
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Subtitle = request.Subtitle,
                ImagePath = request.ImagePath,
                MobileImagePath = request.MobileImagePath,
                AltText = request.AltText,
                MobileAltText = request.MobileAltText,
                LinkUrl = request.LinkUrl,
                LinkText = request.LinkText,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.HomeSlides.AddAsync(slide);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(slide.Id);
        }

        public async Task<AdminHomeSlideDto> UpdateAsync(Guid id, UpdateHomeSlideRequestDto request)
        {
            Normalize(request);
            Validate(request);

            var slide = await _dbContext.HomeSlides.FirstOrDefaultAsync(x => x.Id == id);

            if (slide is null)
                throw new NotFoundException("اسلاید یافت نشد.");

            slide.Title = request.Title;
            slide.Subtitle = request.Subtitle;
            slide.ImagePath = request.ImagePath;
            slide.MobileImagePath = request.MobileImagePath;
            slide.AltText = request.AltText;
            slide.MobileAltText = request.MobileAltText;
            slide.LinkUrl = request.LinkUrl;
            slide.LinkText = request.LinkText;
            slide.SortOrder = request.SortOrder;
            slide.IsActive = request.IsActive;
            slide.StartsAt = request.StartsAt;
            slide.EndsAt = request.EndsAt;
            slide.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(slide.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var slide = await _dbContext.HomeSlides.FirstOrDefaultAsync(x => x.Id == id);

            if (slide is null)
                throw new NotFoundException("اسلاید یافت نشد.");

            _dbContext.HomeSlides.Remove(slide);
            await _dbContext.SaveChangesAsync();
        }

        private static AdminHomeSlideDto Project(HomeSlide x) => new()
        {
            Id = x.Id,
            Title = x.Title,
            Subtitle = x.Subtitle,
            ImagePath = x.ImagePath,
            MobileImagePath = x.MobileImagePath,
            AltText = x.AltText,
            MobileAltText = x.MobileAltText,
            LinkUrl = x.LinkUrl,
            LinkText = x.LinkText,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive,
            StartsAt = x.StartsAt,
            EndsAt = x.EndsAt,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };

        private static void Normalize(CreateHomeSlideRequestDto request)
        {
            request.Title = request.Title?.Trim() ?? string.Empty;
            request.Subtitle = NormalizeNullable(request.Subtitle);
            request.ImagePath = NormalizeNullable(request.ImagePath);
            request.MobileImagePath = NormalizeNullable(request.MobileImagePath);
            request.AltText = NormalizeNullable(request.AltText);
            request.MobileAltText = NormalizeNullable(request.MobileAltText);
            request.LinkUrl = NormalizeNullable(request.LinkUrl);
            request.LinkText = NormalizeNullable(request.LinkText);

            if (request.SortOrder < 0)
                request.SortOrder = 0;
        }

        private static void Validate(CreateHomeSlideRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new BusinessException("عنوان اسلاید الزامی است.");

            if (request.StartsAt.HasValue && request.EndsAt.HasValue && request.StartsAt > request.EndsAt)
                throw new BusinessException("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");

            // A button with no destination is a dead control, and a destination with no label is a
            // button nobody can read. Either both or neither.
            if (!string.IsNullOrWhiteSpace(request.LinkText) && string.IsNullOrWhiteSpace(request.LinkUrl))
                throw new BusinessException("برای متن دکمه باید لینک مقصد هم وارد شود.");
        }

        private static string? NormalizeNullable(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
