using Vitorize.Application.DTOs.Verification;

namespace Vitorize.Application.Interfaces
{
    public interface IVerificationService
    {
        Task<VerificationProfileDto?> GetMyProfileAsync(Guid userId);

        Task<VerificationProfileDto> SubmitAsync(
            Guid userId,
            SubmitVerificationRequestDto request);

        Task<VerificationDocumentDto> AddDocumentAsync(
            Guid userId,
            byte documentType,
            string filePath);

        /// <summary>
        /// حذف مدرک احراز هویتِ خودِ کاربر، فقط زمانی که پرونده هنوز در وضعیت در انتظار بررسی است.
        /// </summary>
        Task DeleteDocumentAsync(Guid userId, Guid documentId);

        Task<List<VerificationProfileDto>> GetAllAsync();

        Task<VerificationProfileDto> GetByIdAsync(Guid profileId);

        Task<VerificationProfileDto> ReviewAsync(
            Guid profileId,
            Guid adminUserId,
            ReviewVerificationRequestDto request);
    }
}