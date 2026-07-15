using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Brands;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminBrandService : IAdminBrandService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminBrandService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AdminBrandDto>> GetAllAsync()
        {
            return await _dbContext.Brands
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new AdminBrandDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ImagePath = x.ImagePath,
                    ImageAltText = x.ImageAltText,
                    Description = x.Description,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    FocusKeyword = x.FocusKeyword,
                    IsActive = x.IsActive
                })
                .ToListAsync();
        }

        public async Task<AdminBrandDto> GetByIdAsync(Guid id)
        {
            var brand = await _dbContext.Brands
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AdminBrandDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ImagePath = x.ImagePath,
                    ImageAltText = x.ImageAltText,
                    Description = x.Description,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    FocusKeyword = x.FocusKeyword,
                    IsActive = x.IsActive
                })
                .FirstOrDefaultAsync();

            if (brand == null)
                throw new NotFoundException("برند یافت نشد.");

            return brand;
        }

        public async Task<AdminBrandDto> CreateAsync(CreateBrandRequestDto request)
        {
            await ValidateAsync(request.Title, request.Slug, null);

            var brand = new Brand
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Slug = request.Slug.Trim().ToLower(),
                ImagePath = request.ImagePath,
                ImageAltText = request.ImageAltText,
                Description = request.Description,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                FocusKeyword = request.FocusKeyword,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Brands.AddAsync(brand);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(brand.Id);
        }

        public async Task<AdminBrandDto> UpdateAsync(Guid id, UpdateBrandRequestDto request)
        {
            var brand = await _dbContext.Brands
                .FirstOrDefaultAsync(x => x.Id == id);

            if (brand == null)
                throw new NotFoundException("برند یافت نشد.");

            await ValidateAsync(request.Title, request.Slug, id);

            brand.Title = request.Title.Trim();
            brand.Slug = request.Slug.Trim().ToLower();
            brand.ImagePath = request.ImagePath;
            brand.ImageAltText = request.ImageAltText;
            brand.Description = request.Description;
            brand.SeoTitle = request.SeoTitle;
            brand.SeoDescription = request.SeoDescription;
            brand.FocusKeyword = request.FocusKeyword;
            brand.UpdatedAt = DateTime.UtcNow;
            brand.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(brand.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var brand = await _dbContext.Brands
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (brand == null)
                throw new NotFoundException("برند یافت نشد.");

            if (brand.Products.Any(x => !x.IsDeleted))
                throw new BusinessException("این برند دارای محصول است و قابل حذف نیست.");

            _dbContext.Brands.Remove(brand);

            await _dbContext.SaveChangesAsync();
        }

        private async Task ValidateAsync(string title, string slug, Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new BusinessException("عنوان برند الزامی است.");

            if (string.IsNullOrWhiteSpace(slug))
                throw new BusinessException("اسلاگ برند الزامی است.");

            var normalizedSlug = slug.Trim().ToLower();

            var slugExists = await _dbContext.Brands.AnyAsync(x =>
                x.Slug == normalizedSlug &&
                (!currentId.HasValue || x.Id != currentId.Value));

            if (slugExists)
                throw new BusinessException("این اسلاگ قبلاً برای برند دیگری ثبت شده است.");
        }
    }
}
