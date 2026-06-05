using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Categories;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminCategoryService : IAdminCategoryService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminCategoryService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AdminCategoryDto>> GetAllAsync()
        {
            return await _dbContext.Categories
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .Select(x => new AdminCategoryDto
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    Title = x.Title,
                    Slug = x.Slug,
                    Description = x.Description,
                    ImagePath = x.ImagePath,
                    Icon = x.Icon,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription
                })
                .ToListAsync();
        }

        public async Task<AdminCategoryDto> GetByIdAsync(Guid id)
        {
            var category = await _dbContext.Categories
                .AsNoTracking()
                .Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => new AdminCategoryDto
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    Title = x.Title,
                    Slug = x.Slug,
                    Description = x.Description,
                    ImagePath = x.ImagePath,
                    Icon = x.Icon,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription
                })
                .FirstOrDefaultAsync();

            if (category == null)
                throw new NotFoundException("دسته‌بندی یافت نشد.");

            return category;
        }

        public async Task<AdminCategoryDto> CreateAsync(CreateCategoryRequestDto request)
        {
            await ValidateAsync(request.Title, request.Slug, request.ParentId, null);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                ParentId = request.ParentId,
                Title = request.Title.Trim(),
                Slug = request.Slug.Trim().ToLower(),
                Description = request.Description,
                ImagePath = request.ImagePath,
                Icon = request.Icon,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _dbContext.Categories.AddAsync(category);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(category.Id);
        }

        public async Task<AdminCategoryDto> UpdateAsync(Guid id, UpdateCategoryRequestDto request)
        {
            var category = await _dbContext.Categories
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (category == null)
                throw new NotFoundException("دسته‌بندی یافت نشد.");

            await ValidateAsync(request.Title, request.Slug, request.ParentId, id);

            category.ParentId = request.ParentId;
            category.Title = request.Title.Trim();
            category.Slug = request.Slug.Trim().ToLower();
            category.Description = request.Description;
            category.ImagePath = request.ImagePath;
            category.Icon = request.Icon;
            category.SortOrder = request.SortOrder;
            category.IsActive = request.IsActive;
            category.SeoTitle = request.SeoTitle;
            category.SeoDescription = request.SeoDescription;
            category.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(category.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var category = await _dbContext.Categories
                .Include(x => x.Products)
                .Include(x => x.InverseParent)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (category == null)
                throw new NotFoundException("دسته‌بندی یافت نشد.");

            if (category.Products.Any(x => !x.IsDeleted))
                throw new BusinessException("این دسته‌بندی دارای محصول فعال است و قابل حذف نیست.");

            if (category.InverseParent.Any(x => !x.IsDeleted))
                throw new BusinessException("این دسته‌بندی دارای زیرمجموعه است و قابل حذف نیست.");

            category.IsDeleted = true;
            category.DeletedAt = DateTime.UtcNow;
            category.IsActive = false;

            await _dbContext.SaveChangesAsync();
        }

        private async Task ValidateAsync(
            string title,
            string slug,
            Guid? parentId,
            Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new BusinessException("عنوان دسته‌بندی الزامی است.");

            if (string.IsNullOrWhiteSpace(slug))
                throw new BusinessException("اسلاگ دسته‌بندی الزامی است.");

            var normalizedSlug = slug.Trim().ToLower();

            var slugExists = await _dbContext.Categories.AnyAsync(x =>
                x.Slug == normalizedSlug &&
                !x.IsDeleted &&
                (!currentId.HasValue || x.Id != currentId.Value));

            if (slugExists)
                throw new BusinessException("این اسلاگ قبلاً برای دسته‌بندی دیگری ثبت شده است.");

            if (parentId.HasValue)
            {
                if (currentId.HasValue && parentId.Value == currentId.Value)
                    throw new BusinessException("دسته‌بندی نمی‌تواند والد خودش باشد.");

                var parentExists = await _dbContext.Categories.AnyAsync(x =>
                    x.Id == parentId.Value &&
                    !x.IsDeleted);

                if (!parentExists)
                    throw new BusinessException("دسته‌بندی والد معتبر نیست.");
            }
        }
    }
}