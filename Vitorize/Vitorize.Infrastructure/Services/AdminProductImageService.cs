using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.ProductImages;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminProductImageService : IAdminProductImageService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminProductImageService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AdminProductImageDto>> GetByProductIdAsync(Guid productId)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == productId);

            if (product == null)
                throw new NotFoundException("محصول یافت نشد.");

            var images = await _dbContext.ProductImages
                .AsNoTracking()
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new AdminProductImageDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    ImagePath = x.ImagePath,
                    AltText = x.AltText,
                    SortOrder = x.SortOrder,
                    CreatedAt = x.CreatedAt,
                    IsThumbnail = product.ThumbnailImagePath != null &&
                                  product.ThumbnailImagePath == x.ImagePath
                })
                .ToListAsync();

            return images;
        }

        public async Task<AdminProductImageDto> CreateAsync(
            Guid productId,
            CreateProductImageRequestDto request)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(x => x.Id == productId);

            if (product == null)
                throw new NotFoundException("محصول یافت نشد.");

            var image = new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                ImagePath = request.ImagePath.Trim(),
                AltText = request.AltText,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ProductImages.Add(image);

            if (request.SetAsThumbnail || string.IsNullOrWhiteSpace(product.ThumbnailImagePath))
            {
                product.ThumbnailImagePath = image.ImagePath;
                product.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            return ToDto(image, product.ThumbnailImagePath);
        }

        public async Task<AdminProductImageDto> UpdateAsync(
            Guid imageId,
            UpdateProductImageRequestDto request)
        {
            var image = await _dbContext.ProductImages
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == imageId);

            if (image == null)
                throw new NotFoundException("تصویر محصول یافت نشد.");

            image.AltText = request.AltText;
            image.SortOrder = request.SortOrder;

            await _dbContext.SaveChangesAsync();

            return ToDto(image, image.Product.ThumbnailImagePath);
        }

        public async Task SetAsThumbnailAsync(Guid imageId)
        {
            var image = await _dbContext.ProductImages
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == imageId);

            if (image == null)
                throw new NotFoundException("تصویر محصول یافت نشد.");

            image.Product.ThumbnailImagePath = image.ImagePath;
            image.Product.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid imageId)
        {
            var image = await _dbContext.ProductImages
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == imageId);

            if (image == null)
                throw new NotFoundException("تصویر محصول یافت نشد.");

            var product = image.Product;
            var wasThumbnail = product.ThumbnailImagePath == image.ImagePath;

            _dbContext.ProductImages.Remove(image);

            if (wasThumbnail)
            {
                var nextImage = await _dbContext.ProductImages
                    .Where(x => x.ProductId == product.Id && x.Id != image.Id)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.CreatedAt)
                    .FirstOrDefaultAsync();

                product.ThumbnailImagePath = nextImage?.ImagePath;
                product.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }

        private static AdminProductImageDto ToDto(
            ProductImage image,
            string? thumbnailImagePath)
        {
            return new AdminProductImageDto
            {
                Id = image.Id,
                ProductId = image.ProductId,
                ImagePath = image.ImagePath,
                AltText = image.AltText,
                SortOrder = image.SortOrder,
                CreatedAt = image.CreatedAt,
                IsThumbnail = !string.IsNullOrWhiteSpace(thumbnailImagePath) &&
                              thumbnailImagePath == image.ImagePath
            };
        }
    }
}