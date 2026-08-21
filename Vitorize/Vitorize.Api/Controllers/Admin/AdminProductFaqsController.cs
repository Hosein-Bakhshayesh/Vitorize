using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    /// <summary>
    /// Product-owned FAQ entries. They live in the same table as the site-wide FAQ, told apart by
    /// ProductId, so this controller exists to bind that scope to the route rather than trusting a
    /// caller-supplied product id: the product is always taken from the URL, and listing is always
    /// filtered to it. Whoever may edit a product may edit its questions, hence AdminOnly rather
    /// than the site-settings policy that guards the global FAQ screen.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/products/{productId:guid}/faqs")]
    public class AdminProductFaqsController : ControllerBase
    {
        private readonly IAdminFaqService _faqService;

        public AdminProductFaqsController(IAdminFaqService faqService)
        {
            _faqService = faqService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminFaqDto>>>> GetForProduct(Guid productId)
        {
            var result = await _faqService.GetByProductAsync(productId);

            return Ok(ApiResult<List<AdminFaqDto>>.Success(
                result,
                "سوالات متداول این محصول دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminFaqDto>>> Create(Guid productId, CreateFaqRequestDto request)
        {
            // The route owns the scope. Anything the body claimed is discarded so this endpoint can
            // never create or move a site-wide entry.
            request.ProductId = productId;

            var result = await _faqService.CreateAsync(request);

            return Ok(ApiResult<AdminFaqDto>.Success(result, "پرسش محصول ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminFaqDto>>> Update(
            Guid productId, Guid id, UpdateFaqRequestDto request)
        {
            await EnsureBelongsToProductAsync(productId, id);
            request.ProductId = productId;

            var result = await _faqService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminFaqDto>.Success(result, "پرسش محصول ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid productId, Guid id)
        {
            await EnsureBelongsToProductAsync(productId, id);

            await _faqService.DeleteAsync(id);

            return Ok(ApiResult.Success("پرسش محصول حذف شد."));
        }

        /// <summary>
        /// Refuses to edit or delete through a product that does not own the entry, so one product's
        /// route cannot reach another product's questions or the site-wide list.
        /// </summary>
        private async Task EnsureBelongsToProductAsync(Guid productId, Guid faqId)
        {
            var faq = await _faqService.GetByIdAsync(faqId);

            if (faq.ProductId != productId)
                throw new Vitorize.Shared.Exceptions.NotFoundException("پرسش این محصول یافت نشد.");
        }
    }
}
