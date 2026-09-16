using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
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
            var categories = await _dbContext.Categories
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
                    ImageAltText = x.ImageAltText,
                    Icon = x.Icon,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    FocusKeyword = x.FocusKeyword
                })
                .ToListAsync();

            await ApplyCountsAsync(categories);

            return categories;
        }

        /// <summary>
        /// Fills the three count columns of the category list.
        /// <para>
        /// The subtree figure is a DISTINCT over a whole branch, which no grouped query can express
        /// without a recursive CTE, and a per-category sum would over-count any product filed under
        /// two categories of the same branch. Since a catalogue has few categories and this screen
        /// already loads all of them, the product links are read once and folded here instead of
        /// pushing provider-specific SQL into an admin list.
        /// </para>
        /// </summary>
        private async Task ApplyCountsAsync(List<AdminCategoryDto> categories)
        {
            if (categories.Count == 0)
                return;

            // Only products a customer could actually reach count: an inactive or deleted product
            // does not stop a category page from looking empty.
            var primaryLinks = await _dbContext.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(p => new { p.CategoryId, ProductId = p.Id })
                .ToListAsync();

            var secondaryLinks = await _dbContext.ProductCategories
                .AsNoTracking()
                .Where(pc => !pc.Product.IsDeleted && pc.Product.IsActive)
                .Select(pc => new { pc.CategoryId, pc.ProductId })
                .ToListAsync();

            var directProducts = new Dictionary<Guid, HashSet<Guid>>();
            foreach (var link in primaryLinks.Concat(secondaryLinks))
            {
                if (!directProducts.TryGetValue(link.CategoryId, out var set))
                    directProducts[link.CategoryId] = set = new HashSet<Guid>();
                set.Add(link.ProductId);
            }

            var children = CategoryHierarchy.ChildrenByParent(
                categories.Select(c => new CategoryNode(c.Id, c.ParentId)));

            foreach (var category in categories)
            {
                category.ProductCount = directProducts.TryGetValue(category.Id, out var own) ? own.Count : 0;
                category.ChildrenCount = children.TryGetValue(category.Id, out var kids) ? kids.Count : 0;

                // Union rather than sum: a product filed under two categories of one branch is
                // still one product on the page that branch leads to.
                var reachable = new HashSet<Guid>();
                foreach (var id in CategoryHierarchy.Branch(children, category.Id))
                    if (directProducts.TryGetValue(id, out var set))
                        reachable.UnionWith(set);

                category.SubtreeProductCount = reachable.Count;
            }
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
                    ImageAltText = x.ImageAltText,
                    Icon = x.Icon,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    FocusKeyword = x.FocusKeyword
                })
                .FirstOrDefaultAsync();

            if (category == null)
                throw new NotFoundException("دسته‌بندی یافت نشد.");

            return category;
        }

        public async Task<AdminCategoryDto> CreateAsync(CreateCategoryRequestDto request)
        {
            await ValidateAsync(request.Title, request.Slug, request.ParentId, null);
            request.Icon = LucideIconRules.NormalizeOptional(request.Icon);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                ParentId = request.ParentId,
                Title = request.Title.Trim(),
                Slug = request.Slug.Trim().ToLower(),
                Description = request.Description,
                ImagePath = request.ImagePath,
                ImageAltText = request.ImageAltText,
                Icon = request.Icon,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                FocusKeyword = request.FocusKeyword,
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
            request.Icon = LucideIconRules.NormalizeOptional(request.Icon);

            category.ParentId = request.ParentId;
            category.Title = request.Title.Trim();
            category.Slug = request.Slug.Trim().ToLower();
            category.Description = request.Description;
            category.ImagePath = request.ImagePath;
            category.ImageAltText = request.ImageAltText;
            category.Icon = request.Icon;
            category.SortOrder = request.SortOrder;
            category.IsActive = request.IsActive;
            category.SeoTitle = request.SeoTitle;
            category.SeoDescription = request.SeoDescription;
            category.FocusKeyword = request.FocusKeyword;
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

                var nextParentId = parentId;
                var visited = new HashSet<Guid>();
                while (nextParentId.HasValue)
                {
                    if (!visited.Add(nextParentId.Value))
                        throw new BusinessException("ساختار والد دسته‌بندی دارای حلقه است.");
                    if (currentId.HasValue && nextParentId.Value == currentId.Value)
                        throw new BusinessException("دسته‌بندی نمی‌تواند یکی از زیرمجموعه‌های خودش را والد قرار دهد.");

                    var parent = await _dbContext.Categories
                        .AsNoTracking()
                        .Where(x => x.Id == nextParentId.Value && !x.IsDeleted)
                        .Select(x => new { x.ParentId })
                        .FirstOrDefaultAsync();
                    if (parent is null)
                        throw new BusinessException("دسته‌بندی والد معتبر نیست.");

                    nextParentId = parent.ParentId;
                }
            }
        }
    }
}
