using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    /// <summary>
    /// FIX-14 Admin FAQ management over the existing structured <see cref="Faq"/> entity.
    /// Answers are plain text: nothing here accepts or emits HTML, and the storefront renders the
    /// answer HTML-encoded, so no sanitiser is involved.
    /// </summary>
    public class AdminFaqService : IAdminFaqService
    {
        private const int MaximumQuestionLength = 500;
        private const int MaximumAnswerLength = 4000;

        private readonly VitorizeDbContext _dbContext;

        public AdminFaqService(VitorizeDbContext dbContext) => _dbContext = dbContext;

        public async Task<List<AdminFaqDto>> GetAllAsync()
        {
            return await _dbContext.Faqs
                .AsNoTracking()
                // The general FAQ screen manages the site-wide entries; product answers are edited
                // on their product, so they must not appear in this list.
                .Where(x => x.ProductId == null)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => Map(x))
                .ToListAsync();
        }

        public async Task<List<AdminFaqDto>> GetByProductAsync(Guid productId)
        {
            return await _dbContext.Faqs
                .AsNoTracking()
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => Map(x))
                .ToListAsync();
        }

        public async Task<AdminFaqDto> GetByIdAsync(Guid id)
        {
            var faq = await _dbContext.Faqs.AsNoTracking()
                .Where(x => x.Id == id).Select(x => Map(x)).FirstOrDefaultAsync();

            if (faq == null)
                throw new NotFoundException("پرسش یافت نشد.");

            return faq;
        }

        public async Task<AdminFaqDto> CreateAsync(CreateFaqRequestDto request)
        {
            Validate(request);

            var faq = new Faq
            {
                Id = Guid.NewGuid(),
                Question = request.Question.Trim(),
                Answer = FaqAnswerText.Normalize(request.Answer),
                SortOrder = Math.Max(0, request.SortOrder),
                IsActive = request.IsActive,
                ProductId = request.ProductId,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Faqs.AddAsync(faq);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(faq.Id);
        }

        public async Task<AdminFaqDto> UpdateAsync(Guid id, UpdateFaqRequestDto request)
        {
            Validate(request);

            var faq = await _dbContext.Faqs.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("پرسش یافت نشد.");

            faq.Question = request.Question.Trim();
            faq.Answer = FaqAnswerText.Normalize(request.Answer);
            faq.SortOrder = Math.Max(0, request.SortOrder);
            faq.IsActive = request.IsActive;
            // Ownership is deliberately immutable here: an edit changes the text and its ordering,
            // never which product (or the site-wide list) the entry belongs to.

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(faq.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var faq = await _dbContext.Faqs.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("پرسش یافت نشد.");

            _dbContext.Faqs.Remove(faq);
            await _dbContext.SaveChangesAsync();
        }



        private static void Validate(CreateFaqRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                throw new BusinessException("متن پرسش الزامی است.");
            if (string.IsNullOrWhiteSpace(request.Answer))
                throw new BusinessException("متن پاسخ الزامی است.");
            if (request.Question.Trim().Length > MaximumQuestionLength)
                throw new BusinessException($"پرسش نمی‌تواند بیشتر از {MaximumQuestionLength} نویسه باشد.");
            if (request.Answer.Trim().Length > MaximumAnswerLength)
                throw new BusinessException($"پاسخ نمی‌تواند بیشتر از {MaximumAnswerLength} نویسه باشد.");
        }

        private static AdminFaqDto Map(Faq x) => new()
        {
            Id = x.Id,
            Question = x.Question,
            Answer = x.Answer,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive,
            ProductId = x.ProductId,
            CreatedAt = x.CreatedAt
        };
    }
}
