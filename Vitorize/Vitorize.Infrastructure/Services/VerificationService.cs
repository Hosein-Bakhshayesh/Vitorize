using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class VerificationService : IVerificationService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly ISmsOutboxEnqueuer _smsOutbox;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<VerificationService> _logger;

        public VerificationService(
            VitorizeDbContext dbContext,
            INotificationService notificationService,
            ISmsOutboxEnqueuer smsOutbox,
            IEncryptionService encryptionService,
            ILogger<VerificationService>? logger = null)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _smsOutbox = smsOutbox;
            _encryptionService = encryptionService;
            _logger = logger ?? NullLogger<VerificationService>.Instance;
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

            var protectedData = new ProtectedVerificationData(
                request.FirstName.Trim(), request.LastName.Trim(), request.NationalCode.Trim(),
                request.BirthDate, request.BankCardNumber?.Trim(), request.ShabaNumber?.Trim(),
                request.Address?.Trim(), request.PostalCode?.Trim());
            profile.EncryptedPayload = _encryptionService.Encrypt(JsonSerializer.Serialize(protectedData));
            profile.EncryptionVersion = 2;
            profile.FirstName = "[protected]";
            profile.LastName = "[protected]";
            profile.NationalCode = "[protected]";
            profile.BirthDate = null;
            profile.BankCardNumber = null;
            profile.ShabaNumber = null;
            profile.Address = null;
            profile.PostalCode = null;
            profile.Status = (byte)VerificationStatus.Pending;
            profile.AdminNote = null;
            profile.SubmittedAt = now;
            profile.UpdatedAt = now;

            user.VerificationStatus = (byte)VerificationStatus.Pending;
            user.NationalCode = null;
            user.UpdatedAt = now;

            await _notificationService.CreateAsync(
                userId,
                (byte)NotificationType.VerificationSubmitted,
                "درخواست احراز هویت ثبت شد",
                "درخواست احراز هویت شما ثبت شد و در انتظار بررسی ادمین است.");

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "KYC profile submitted. UserId={UserId} ProfileId={ProfileId} DocumentCount={DocumentCount} EventType={EventType}",
                userId, profile.Id, profile.VerificationDocuments.Count, OperationalEventNames.KycUploaded);

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
            var expectedPrefix = $"kyc-private:{userId:N}/";
            if (!filePath.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
                filePath.Contains("..", StringComparison.Ordinal) || filePath.Length > 500)
                throw new BusinessException("توکن فایل احراز هویت معتبر نیست.");

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

            _logger.LogInformation(
                "KYC document registered. UserId={UserId} ProfileId={ProfileId} FileId={FileId} DocumentType={DocumentType} EventType={EventType}",
                userId, profile.Id, document.Id, documentType, OperationalEventNames.KycUploaded);

            return MapDocument(document);
        }

        public async Task DeleteDocumentAsync(Guid userId, Guid documentId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var document = await _dbContext.VerificationDocuments
                .Include(x => x.UserVerificationProfile)
                .FirstOrDefaultAsync(x => x.Id == documentId);

            if (document == null)
                throw new NotFoundException("مدرک یافت نشد.");

            if (document.UserVerificationProfile.UserId != userId)
                throw new UnauthorizedException("شما اجازه حذف این مدرک را ندارید.");

            if (document.UserVerificationProfile.Status == (byte)VerificationStatus.Verified)
                throw new BusinessException("پرونده شما تأیید شده است و مدارک قابل حذف نیستند.");

            if (document.Status != (byte)VerificationStatus.Pending)
                throw new BusinessException("فقط مدارک در انتظار بررسی قابل حذف هستند.");

            _dbContext.VerificationDocuments.Remove(document);

            document.UserVerificationProfile.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<VerificationProfileDto>> GetAllAsync()
        {
            var profiles = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .AsNoTracking()
                .OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt)
                .ToListAsync();
            return profiles.Select(MapProfile).ToList();
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

            if (request.Approve)
            {
                await _smsOutbox.EnqueueTemplateAsync(
                    user.Mobile,
                    Vitorize.Application.Common.SmsTemplateKeys.VerificationApproved,
                    Vitorize.Application.Models.Sms.SmsBusinessNotificationParameters.VerificationApproved(
                        Vitorize.Application.Common.SmsPublicReference.ForVerification(profile.Id)),
                    purpose: "VerificationApproved",
                    aggregateId: profile.Id,
                    userId: user.Id,
                    createdByUserId: adminUserId,
                    relatedEntityType: "Verification",
                    relatedEntityReference: Vitorize.Application.Common.SmsPublicReference.ForVerification(profile.Id));

                await _notificationService.CreateAsync(
                    user.Id,
                    (byte)NotificationType.VerificationApproved,
                    "احراز هویت تایید شد",
                    "احراز هویت شما با موفقیت تایید شد.");
            }
            else
            {
                await _smsOutbox.EnqueueTemplateAsync(
                    user.Mobile,
                    Vitorize.Application.Common.SmsTemplateKeys.VerificationRejected,
                    Vitorize.Application.Models.Sms.SmsBusinessNotificationParameters.VerificationRejected(
                        Vitorize.Application.Common.SmsPublicReference.ForVerification(profile.Id)),
                    purpose: "VerificationRejected",
                    aggregateId: profile.Id,
                    userId: user.Id,
                    createdByUserId: adminUserId,
                    relatedEntityType: "Verification",
                    relatedEntityReference: Vitorize.Application.Common.SmsPublicReference.ForVerification(profile.Id));

                await _notificationService.CreateAsync(
                    user.Id,
                    (byte)NotificationType.VerificationRejected,
                    "احراز هویت رد شد",
                    string.IsNullOrWhiteSpace(request.AdminNote)
                        ? "درخواست احراز هویت شما رد شد."
                        : $"درخواست احراز هویت شما رد شد. علت: {request.AdminNote}");
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "KYC review completed. UserId={UserId} ProfileId={ProfileId} AdminUserId={AdminUserId} DocumentCount={DocumentCount} Approved={Approved} EventType={EventType}",
                user.Id, profile.Id, adminUserId, profile.VerificationDocuments.Count, request.Approve,
                request.Approve ? OperationalEventNames.KycApproved : OperationalEventNames.KycRejected);

            return MapProfile(profile);
        }

        private VerificationProfileDto MapProfile(UserVerificationProfile profile)
        {
            var data = ReadProtectedData(profile);
            return new VerificationProfileDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                FirstName = data.FirstName,
                LastName = data.LastName,
                NationalCode = data.NationalCode,
                BirthDate = data.BirthDate,
                BankCardNumber = data.BankCardNumber,
                ShabaNumber = data.ShabaNumber,
                Address = data.Address,
                PostalCode = data.PostalCode,
                Status = profile.Status,
                AdminNote = profile.AdminNote,
                SubmittedAt = profile.SubmittedAt,
                Documents = profile.VerificationDocuments
                    .Select(MapDocument)
                    .ToList()
            };
        }

        private ProtectedVerificationData ReadProtectedData(UserVerificationProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.EncryptedPayload))
            {
                var json = _encryptionService.Decrypt(profile.EncryptedPayload);
                return JsonSerializer.Deserialize<ProtectedVerificationData>(json)
                    ?? throw new BusinessException("اطلاعات محافظت‌شده احراز هویت معتبر نیست.");
            }
            return new ProtectedVerificationData(profile.FirstName, profile.LastName,
                profile.NationalCode, profile.BirthDate, profile.BankCardNumber,
                profile.ShabaNumber, profile.Address, profile.PostalCode);
        }

        private static VerificationDocumentDto MapDocument(VerificationDocument document)
        {
            return new VerificationDocumentDto
            {
                Id = document.Id,
                DocumentType = document.DocumentType,
                FilePath = $"/api/verification/documents/{document.Id}/content",
                Status = document.Status,
                AdminNote = document.AdminNote
            };
        }

        private sealed record ProtectedVerificationData(
            string FirstName, string LastName, string NationalCode, DateOnly? BirthDate,
            string? BankCardNumber, string? ShabaNumber, string? Address, string? PostalCode);
    }
}
