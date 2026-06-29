using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Reviews;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class ProductReviewService : IProductReviewService
    {
        private const int MaxCommentLength = 2000;
        private const int MaxTitleLength = 200;

        private readonly VitorizeDbContext _dbContext;

        public ProductReviewService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ProductReviewListResultDto> GetApprovedForProductAsync(
            ProductReviewFilterDto filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (filter.ProductId == Guid.Empty)
                throw new BusinessException("شناسه محصول معتبر نیست.");

            var page = filter.Page <= 0 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 10 : filter.PageSize;
            pageSize = pageSize > 50 ? 50 : pageSize;

            var productExists = await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == filter.ProductId && x.IsActive && !x.IsDeleted,
                    cancellationToken);

            if (!productExists)
                throw new NotFoundException("محصول یافت نشد.");

            var approvedQuery = _dbContext.ProductReviews
                .AsNoTracking()
                .Where(x =>
                    x.ProductId == filter.ProductId &&
                    x.ParentId == null &&
                    x.IsApproved &&
                    !x.IsRejected &&
                    !x.IsDeleted);

            var summary = await BuildSummaryAsync(
                filter.ProductId,
                approvedQuery,
                cancellationToken);

            var totalCount = summary.TotalApprovedReviews;

            var orderedQuery = ApplySort(approvedQuery, filter.Sort);

            var reviews = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProductReviewDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    UserId = x.UserId,
                    UserDisplayName = x.User.FullName,
                    Title = x.Title,
                    Comment = x.Comment,
                    Rating = x.Rating,
                    IsBuyer = x.IsBuyer,
                    IsApproved = x.IsApproved,
                    IsRejected = x.IsRejected,
                    LikeCount = x.LikeCount,
                    DislikeCount = x.DislikeCount,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            await PopulateMyVotesAsync(reviews, currentUserId, cancellationToken);

            return new ProductReviewListResultDto
            {
                Summary = summary,
                Reviews = new PagedResult<ProductReviewDto>
                {
                    Items = reviews,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }

        public async Task<ProductReviewSummaryDto> GetSummaryAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            if (productId == Guid.Empty)
                throw new BusinessException("شناسه محصول معتبر نیست.");

            var approvedQuery = _dbContext.ProductReviews
                .AsNoTracking()
                .Where(x =>
                    x.ProductId == productId &&
                    x.ParentId == null &&
                    x.IsApproved &&
                    !x.IsRejected &&
                    !x.IsDeleted);

            return await BuildSummaryAsync(productId, approvedQuery, cancellationToken);
        }

        public async Task<List<ProductReviewDto>> GetMyReviewsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return await _dbContext.ProductReviews
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.ParentId == null &&
                    !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new ProductReviewDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    UserId = x.UserId,
                    UserDisplayName = x.User.FullName,
                    Title = x.Title,
                    Comment = x.Comment,
                    Rating = x.Rating,
                    IsBuyer = x.IsBuyer,
                    IsApproved = x.IsApproved,
                    IsRejected = x.IsRejected,
                    LikeCount = x.LikeCount,
                    DislikeCount = x.DislikeCount,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductReviewDto> CreateAsync(
            Guid userId,
            CreateProductReviewRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var comment = NormalizeComment(request.Comment);
            var title = NormalizeTitle(request.Title);
            ValidateRating(request.Rating);

            var product = await _dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == request.ProductId && x.IsActive && !x.IsDeleted,
                    cancellationToken);

            if (product == null)
                throw new NotFoundException("محصول یافت نشد.");

            var alreadyReviewed = await _dbContext.ProductReviews
                .AnyAsync(
                    x =>
                        x.ProductId == request.ProductId &&
                        x.UserId == userId &&
                        x.ParentId == null &&
                        !x.IsDeleted,
                    cancellationToken);

            if (alreadyReviewed)
                throw new BusinessException("شما قبلاً برای این محصول نظر ثبت کرده‌اید.");

            var isBuyer = await IsBuyerAsync(userId, request.ProductId, cancellationToken);

            var review = new ProductReview
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                UserId = userId,
                ParentId = null,
                Title = title,
                Comment = comment,
                Rating = request.Rating,
                IsApproved = false,
                IsRejected = false,
                IsBuyer = isBuyer,
                LikeCount = 0,
                DislikeCount = 0,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _dbContext.ProductReviews.AddAsync(review, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await GetOwnReviewDtoAsync(review.Id, userId, cancellationToken);
        }

        public async Task<ProductReviewDto> UpdateAsync(
            Guid userId,
            Guid reviewId,
            UpdateProductReviewRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var comment = NormalizeComment(request.Comment);
            var title = NormalizeTitle(request.Title);
            ValidateRating(request.Rating);

            var review = await _dbContext.ProductReviews
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId && !x.IsDeleted,
                    cancellationToken);

            if (review == null)
                throw new NotFoundException("نظر یافت نشد.");

            if (review.UserId != userId)
                throw new UnauthorizedException("شما اجازه ویرایش این نظر را ندارید.");

            if (review.IsApproved)
                throw new BusinessException("نظر تأییدشده قابل ویرایش نیست.");

            review.Title = title;
            review.Comment = comment;
            review.Rating = request.Rating;
            // پس از ویرایش، نظر مجدداً در صف بررسی قرار می‌گیرد.
            review.IsApproved = false;
            review.IsRejected = false;
            review.RejectionReason = null;
            review.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await GetOwnReviewDtoAsync(review.Id, userId, cancellationToken);
        }

        public async Task DeleteAsync(
            Guid userId,
            Guid reviewId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var review = await _dbContext.ProductReviews
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId && !x.IsDeleted,
                    cancellationToken);

            if (review == null)
                throw new NotFoundException("نظر یافت نشد.");

            if (review.UserId != userId)
                throw new UnauthorizedException("شما اجازه حذف این نظر را ندارید.");

            review.IsDeleted = true;
            review.DeletedAt = DateTime.UtcNow;
            review.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<ProductReviewDto> VoteAsync(
            Guid userId,
            Guid reviewId,
            byte voteType,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (!Enum.IsDefined(typeof(ReviewVoteType), voteType))
                throw new BusinessException("نوع رأی معتبر نیست.");

            var review = await _dbContext.ProductReviews
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == reviewId &&
                        x.IsApproved &&
                        !x.IsRejected &&
                        !x.IsDeleted,
                    cancellationToken);

            if (review == null)
                throw new NotFoundException("نظر یافت نشد.");

            if (review.UserId == userId)
                throw new BusinessException("نمی‌توانید به نظر خودتان رأی دهید.");

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var existingVote = await _dbContext.ProductReviewVotes
                .FirstOrDefaultAsync(
                    x => x.ReviewId == reviewId && x.UserId == userId,
                    cancellationToken);

            if (existingVote == null)
            {
                await _dbContext.ProductReviewVotes.AddAsync(
                    new ProductReviewVote
                    {
                        Id = Guid.NewGuid(),
                        ReviewId = reviewId,
                        UserId = userId,
                        VoteType = voteType,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);
            }
            else if (existingVote.VoteType != voteType)
            {
                existingVote.VoteType = voteType;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await RecalculateVoteCountsAsync(review, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetOwnReviewDtoAsync(review.Id, userId, cancellationToken);
        }

        public async Task RemoveVoteAsync(
            Guid userId,
            Guid reviewId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var review = await _dbContext.ProductReviews
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId && !x.IsDeleted,
                    cancellationToken);

            if (review == null)
                throw new NotFoundException("نظر یافت نشد.");

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var existingVote = await _dbContext.ProductReviewVotes
                .FirstOrDefaultAsync(
                    x => x.ReviewId == reviewId && x.UserId == userId,
                    cancellationToken);

            if (existingVote == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            _dbContext.ProductReviewVotes.Remove(existingVote);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await RecalculateVoteCountsAsync(review, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private async Task RecalculateVoteCountsAsync(
            ProductReview review,
            CancellationToken cancellationToken)
        {
            var likeCount = await _dbContext.ProductReviewVotes
                .CountAsync(
                    x => x.ReviewId == review.Id &&
                         x.VoteType == (byte)ReviewVoteType.Helpful,
                    cancellationToken);

            var dislikeCount = await _dbContext.ProductReviewVotes
                .CountAsync(
                    x => x.ReviewId == review.Id &&
                         x.VoteType == (byte)ReviewVoteType.Unhelpful,
                    cancellationToken);

            review.LikeCount = likeCount;
            review.DislikeCount = dislikeCount;
            review.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<ProductReviewSummaryDto> BuildSummaryAsync(
            Guid productId,
            IQueryable<ProductReview> approvedQuery,
            CancellationToken cancellationToken)
        {
            var ratingGroups = await approvedQuery
                .GroupBy(x => x.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var summary = new ProductReviewSummaryDto
            {
                ProductId = productId,
                FiveStarCount = ratingGroups.FirstOrDefault(x => x.Rating == 5)?.Count ?? 0,
                FourStarCount = ratingGroups.FirstOrDefault(x => x.Rating == 4)?.Count ?? 0,
                ThreeStarCount = ratingGroups.FirstOrDefault(x => x.Rating == 3)?.Count ?? 0,
                TwoStarCount = ratingGroups.FirstOrDefault(x => x.Rating == 2)?.Count ?? 0,
                OneStarCount = ratingGroups.FirstOrDefault(x => x.Rating == 1)?.Count ?? 0
            };

            var totalCount = ratingGroups.Sum(x => x.Count);
            summary.TotalApprovedReviews = totalCount;

            summary.AverageRating = totalCount == 0
                ? 0
                : Math.Round(
                    ratingGroups.Sum(x => (double)x.Rating * x.Count) / totalCount,
                    2);

            return summary;
        }

        private async Task PopulateMyVotesAsync(
            List<ProductReviewDto> reviews,
            Guid? currentUserId,
            CancellationToken cancellationToken)
        {
            if (!currentUserId.HasValue || reviews.Count == 0)
                return;

            var reviewIds = reviews.Select(x => x.Id).ToList();

            var votes = await _dbContext.ProductReviewVotes
                .AsNoTracking()
                .Where(x =>
                    x.UserId == currentUserId.Value &&
                    reviewIds.Contains(x.ReviewId))
                .Select(x => new { x.ReviewId, x.VoteType })
                .ToListAsync(cancellationToken);

            if (votes.Count == 0)
                return;

            var voteMap = votes.ToDictionary(x => x.ReviewId, x => x.VoteType);

            foreach (var review in reviews)
            {
                if (voteMap.TryGetValue(review.Id, out var voteType))
                    review.MyVote = voteType;
            }
        }

        private async Task<ProductReviewDto> GetOwnReviewDtoAsync(
            Guid reviewId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var dto = await _dbContext.ProductReviews
                .AsNoTracking()
                .Where(x => x.Id == reviewId)
                .Select(x => new ProductReviewDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    UserId = x.UserId,
                    UserDisplayName = x.User.FullName,
                    Title = x.Title,
                    Comment = x.Comment,
                    Rating = x.Rating,
                    IsBuyer = x.IsBuyer,
                    IsApproved = x.IsApproved,
                    IsRejected = x.IsRejected,
                    LikeCount = x.LikeCount,
                    DislikeCount = x.DislikeCount,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (dto == null)
                throw new NotFoundException("نظر یافت نشد.");

            var myVote = await _dbContext.ProductReviewVotes
                .AsNoTracking()
                .Where(x => x.ReviewId == reviewId && x.UserId == userId)
                .Select(x => (byte?)x.VoteType)
                .FirstOrDefaultAsync(cancellationToken);

            dto.MyVote = myVote;

            return dto;
        }

        private async Task<bool> IsBuyerAsync(
            Guid userId,
            Guid productId,
            CancellationToken cancellationToken)
        {
            return await _dbContext.OrderItems
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.ProductId == productId &&
                        x.Order.UserId == userId &&
                        (x.Order.Status == (byte)OrderStatus.Completed ||
                         x.Order.Status == (byte)OrderStatus.Processing),
                    cancellationToken);
        }

        private static IQueryable<ProductReview> ApplySort(
            IQueryable<ProductReview> query,
            string? sort)
        {
            return (sort?.Trim().ToLowerInvariant()) switch
            {
                "oldest" => query.OrderBy(x => x.CreatedAt),
                "highest" => query.OrderByDescending(x => x.Rating)
                    .ThenByDescending(x => x.CreatedAt),
                "lowest" => query.OrderBy(x => x.Rating)
                    .ThenByDescending(x => x.CreatedAt),
                "helpful" => query.OrderByDescending(x => x.LikeCount)
                    .ThenByDescending(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };
        }

        private static string NormalizeComment(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
                throw new BusinessException("متن نظر الزامی است.");

            var normalized = comment.Trim();

            if (normalized.Length > MaxCommentLength)
                throw new BusinessException(
                    $"متن نظر نمی‌تواند بیش از {MaxCommentLength} کاراکتر باشد.");

            return normalized;
        }

        private static string? NormalizeTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var normalized = title.Trim();

            if (normalized.Length > MaxTitleLength)
                throw new BusinessException(
                    $"عنوان نظر نمی‌تواند بیش از {MaxTitleLength} کاراکتر باشد.");

            return normalized;
        }

        private static void ValidateRating(byte rating)
        {
            if (rating < 1 || rating > 5)
                throw new BusinessException("امتیاز باید بین ۱ تا ۵ باشد.");
        }
    }
}
