using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Reviews;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminProductReviewService : IAdminProductReviewService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly INotificationService _notificationService;

        public AdminProductReviewService(
            VitorizeDbContext dbContext,
            INotificationService notificationService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
        }

        public async Task<PagedResult<AdminProductReviewDto>> GetAllAsync(
            AdminProductReviewFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var page = filter.Page <= 0 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;
            pageSize = pageSize > 100 ? 100 : pageSize;

            var query = _dbContext.ProductReviews
                .AsNoTracking()
                .Where(x => x.ParentId == null && !x.IsDeleted);

            if (filter.ProductId.HasValue)
                query = query.Where(x => x.ProductId == filter.ProductId.Value);

            if (filter.UserId.HasValue)
                query = query.Where(x => x.UserId == filter.UserId.Value);

            if (filter.Rating.HasValue)
                query = query.Where(x => x.Rating == filter.Rating.Value);

            if (filter.IsApproved.HasValue)
                query = query.Where(x => x.IsApproved == filter.IsApproved.Value);

            if (filter.IsRejected.HasValue)
                query = query.Where(x => x.IsRejected == filter.IsRejected.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();

                query = query.Where(x =>
                    x.Comment.Contains(search) ||
                    (x.Title != null && x.Title.Contains(search)));
            }

            if (filter.FromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                query = query.Where(x => x.CreatedAt <= filter.ToDate.Value);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapAdminProjection)
                .ToListAsync(cancellationToken);

            return new PagedResult<AdminProductReviewDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AdminProductReviewDto> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var review = await _dbContext.ProductReviews
                .AsNoTracking()
                .Where(x => x.Id == id && !x.IsDeleted)
                .Select(MapAdminProjection)
                .FirstOrDefaultAsync(cancellationToken);

            if (review == null)
                throw new NotFoundException("نظر یافت نشد.");

            return review;
        }

        public async Task<AdminProductReviewDto> ApproveAsync(
            Guid adminUserId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var review = await GetEntityAsync(id, cancellationToken);

            review.IsApproved = true;
            review.IsRejected = false;
            review.RejectionReason = null;
            review.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _notificationService.CreateAsync(
                review.UserId,
                (byte)NotificationType.SystemMessage,
                "نظر شما تأیید شد",
                "نظر شما درباره محصول تأیید و منتشر شد. از همراهی شما سپاسگزاریم.");

            return await GetByIdAsync(id, cancellationToken);
        }

        public async Task<AdminProductReviewDto> RejectAsync(
            Guid adminUserId,
            Guid id,
            RejectProductReviewRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessException("دلیل رد نظر الزامی است.");

            var reason = request.Reason.Trim();

            if (reason.Length > 500)
                throw new BusinessException("دلیل رد نظر نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.");

            var review = await GetEntityAsync(id, cancellationToken);

            review.IsApproved = false;
            review.IsRejected = true;
            review.RejectionReason = reason;
            review.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _notificationService.CreateAsync(
                review.UserId,
                (byte)NotificationType.SystemMessage,
                "نظر شما تأیید نشد",
                $"نظر شما درباره محصول تأیید نشد. دلیل: {reason}");

            return await GetByIdAsync(id, cancellationToken);
        }

        public async Task DeleteAsync(
            Guid adminUserId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var review = await GetEntityAsync(id, cancellationToken);

            review.IsDeleted = true;
            review.DeletedAt = DateTime.UtcNow;
            review.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<ProductReview> GetEntityAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var review = await _dbContext.ProductReviews
                .FirstOrDefaultAsync(
                    x => x.Id == id && !x.IsDeleted,
                    cancellationToken);

            if (review == null)
                throw new NotFoundException("نظر یافت نشد.");

            return review;
        }

        // پروجکشن قابل ترجمه به SQL (JOIN به Product و User) برای استفاده مجدد در لیست و جزئیات.
        private static readonly Expression<Func<ProductReview, AdminProductReviewDto>> MapAdminProjection =
            x => new AdminProductReviewDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductTitle = x.Product.Title,
                UserId = x.UserId,
                UserFullName = x.User.FullName,
                UserMobile = x.User.Mobile,
                Title = x.Title,
                Comment = x.Comment,
                Rating = x.Rating,
                IsApproved = x.IsApproved,
                IsRejected = x.IsRejected,
                RejectionReason = x.RejectionReason,
                IsBuyer = x.IsBuyer,
                LikeCount = x.LikeCount,
                DislikeCount = x.DislikeCount,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            };
    }
}
