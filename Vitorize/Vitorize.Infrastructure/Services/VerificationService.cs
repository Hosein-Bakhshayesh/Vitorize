using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class VerificationService : IVerificationService
    {
        private readonly VitorizeDbContext _dbContext;

        public VerificationService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<VerificationProfileDto?> GetMyProfileAsync(Guid userId)
        {
            var profile = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            return profile == null ? null : MapProfile(profile);
        }

        public async Task<VerificationProfileDto> SubmitAsync(
            Guid userId,
            SubmitVerificationRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (string.IsNullOrWhiteSpace(request.FirstName))
                throw new BusinessException("نام الزامی است.");

            if (string.IsNullOrWhiteSpace(request.LastName))
                throw new BusinessException("نام خانوادگی الزامی است.");

            if (string.IsNullOrWhiteSpace(request.NationalCode))
                throw new BusinessException("کد ملی الزامی است.");

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var now = DateTime.UtcNow;

            var profile = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (profile == null)
            {
                profile = new UserVerificationProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = now
                };

                await _dbContext.UserVerificationProfiles.AddAsync(profile);
            }

            profile.FirstName = request.FirstName.Trim();
            profile.LastName = request.LastName.Trim();
            profile.NationalCode = request.NationalCode.Trim();
            profile.BirthDate = request.BirthDate;
            profile.BankCardNumber = request.BankCardNumber?.Trim();
            profile.ShabaNumber = request.ShabaNumber?.Trim();
            profile.Address = request.Address?.Trim();
            profile.PostalCode = request.PostalCode?.Trim();
            profile.Status = (byte)VerificationStatus.Pending;
            profile.AdminNote = null;
            profile.SubmittedAt = now;
            profile.UpdatedAt = now;

            user.VerificationStatus = (byte)VerificationStatus.Pending;
            user.NationalCode = profile.NationalCode;
            user.UpdatedAt = now;

            await _dbContext.SaveChangesAsync();

            return MapProfile(profile);
        }

        public async Task<VerificationDocumentDto> AddDocumentAsync(
            Guid userId,
            byte documentType,
            string filePath)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (string.IsNullOrWhiteSpace(filePath))
                throw new BusinessException("مسیر فایل معتبر نیست.");

            var profile = await _dbContext.UserVerificationProfiles
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (profile == null)
                throw new BusinessException("ابتدا اطلاعات احراز هویت را ثبت کنید.");

            var now = DateTime.UtcNow;

            var document = new VerificationDocument
            {
                Id = Guid.NewGuid(),
                UserVerificationProfileId = profile.Id,
                DocumentType = documentType,
                FilePath = filePath.Trim(),
                Status = (byte)VerificationStatus.Pending,
                CreatedAt = now
            };

            profile.Status = (byte)VerificationStatus.Pending;
            profile.UpdatedAt = now;

            var user = await _dbContext.Users.FirstAsync(x => x.Id == userId);
            user.VerificationStatus = (byte)VerificationStatus.Pending;
            user.UpdatedAt = now;

            await _dbContext.VerificationDocuments.AddAsync(document);
            await _dbContext.SaveChangesAsync();

            return MapDocument(document);
        }

        public async Task<List<VerificationProfileDto>> GetAllAsync()
        {
            return await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .AsNoTracking()
                .OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt)
                .Select(x => MapProfile(x))
                .ToListAsync();
        }

        public async Task<VerificationProfileDto> GetByIdAsync(Guid profileId)
        {
            var profile = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == profileId);

            if (profile == null)
                throw new NotFoundException("پرونده احراز هویت یافت نشد.");

            return MapProfile(profile);
        }

        public async Task<VerificationProfileDto> ReviewAsync(
            Guid profileId,
            Guid adminUserId,
            ReviewVerificationRequestDto request)
        {
            var profile = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .FirstOrDefaultAsync(x => x.Id == profileId);

            if (profile == null)
                throw new NotFoundException("پرونده احراز هویت یافت نشد.");

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == profile.UserId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var now = DateTime.UtcNow;
            var status = request.Approve
                ? (byte)VerificationStatus.Verified
                : (byte)VerificationStatus.Rejected;

            profile.Status = status;
            profile.AdminNote = request.AdminNote;
            profile.ReviewedByAdminId = adminUserId;
            profile.ReviewedAt = now;
            profile.UpdatedAt = now;

            user.VerificationStatus = status;
            user.UpdatedAt = now;

            foreach (var document in profile.VerificationDocuments)
            {
                document.Status = status;
                document.ReviewedByAdminId = adminUserId;
                document.ReviewedAt = now;
                document.AdminNote = request.AdminNote;
            }

            await _dbContext.SaveChangesAsync();

            return MapProfile(profile);
        }

        private static VerificationProfileDto MapProfile(UserVerificationProfile profile)
        {
            return new VerificationProfileDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                NationalCode = profile.NationalCode,
                BirthDate = profile.BirthDate,
                BankCardNumber = profile.BankCardNumber,
                ShabaNumber = profile.ShabaNumber,
                Address = profile.Address,
                PostalCode = profile.PostalCode,
                Status = profile.Status,
                AdminNote = profile.AdminNote,
                SubmittedAt = profile.SubmittedAt,
                Documents = profile.VerificationDocuments
                    .Select(MapDocument)
                    .ToList()
            };
        }

        private static VerificationDocumentDto MapDocument(VerificationDocument document)
        {
            return new VerificationDocumentDto
            {
                Id = document.Id,
                DocumentType = document.DocumentType,
                FilePath = document.FilePath,
                Status = document.Status,
                AdminNote = document.AdminNote
            };
        }
    }
}