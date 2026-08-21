using Vitorize.Application.DTOs.Admin.Content;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminFaqService
    {
        Task<List<AdminFaqDto>> GetAllAsync();

        /// <summary>Only the given product's entries, in administrator order.</summary>
        Task<List<AdminFaqDto>> GetByProductAsync(Guid productId);

        Task<AdminFaqDto> GetByIdAsync(Guid id);

        Task<AdminFaqDto> CreateAsync(CreateFaqRequestDto request);

        Task<AdminFaqDto> UpdateAsync(Guid id, UpdateFaqRequestDto request);

        Task DeleteAsync(Guid id);
    }
}
