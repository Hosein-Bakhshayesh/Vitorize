using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Common;
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
        private readonly IOrderItemKycLifecycleCoordinator? _lifecycleCoordinator;
        private readonly IOrderItemFulfillmentReleaseService? _fulfillmentReleaseService;
        private readonly IOrderItemKycDeadlineService? _deadlineService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<VerificationService> _logger;

        public VerificationService(
            VitorizeDbContext dbContext,
            INotificationService notificationService,
            ISmsOutboxEnqueuer smsOutbox,
            IEncryptionService encryptionService,
            ILogger<VerificationService>? logger = null,
            IOrderItemKycLifecycleCoordinator? lifecycleCoordinator = null,
            IOrderItemFulfillmentReleaseService? fulfillmentReleaseService = null,
            IOrderItemKycDeadlineService? deadlineService = null,
            TimeProvider? timeProvider = null)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _smsOutbox = smsOutbox;
            _encryptionService = encryptionService;
            _logger = logger ?? NullLogger<VerificationService>.Instance;
            _lifecycleCoordinator = lifecycleCoordinator;
            _fulfillmentReleaseService = fulfillmentReleaseService;
            _deadlineService = deadlineService;
            _timeProvider = timeProvider ?? TimeProvider.System;
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

            if (!request.BirthDate.HasValue)
                throw new BusinessException("تاریخ تولد الزامی است.");

            var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date);
            if (!VerificationBirthDateRules.IsWithinRange(request.BirthDate.Value, today))
                throw new BusinessException("تاریخ تولد واردشده معتبر نیست.");

            var nationalCode = NormalizeNationalCode(request.NationalCode);
            if (nationalCode.Length != 10 || nationalCode.Any(static value => !char.IsAsciiDigit(value)))
                throw new BusinessException("کد ملی باید دقیقاً ۱۰ رقم باشد.");

            if (!request.RegisteredMobileBelongsToCardHolder.HasValue)
                throw new BusinessException("مشخص کنید شماره ثبت‌نام به نام صاحب کارت بانکی است یا خیر.");

            string? cardHolderMobile = null;
            if (!request.RegisteredMobileBelongsToCardHolder.Value)
            {
                if (!IranMobile.TryNormalize(request.CardHolderMobile, out var normalizedMobile))
                    throw new BusinessException("شماره تماس صاحب کارت بانکی معتبر نیست.");
                cardHolderMobile = normalizedMobile;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"verification:user:{userId:N}");
            var committed = false;
            try
            {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var enforcement = _deadlineService is null
                ? new CustomerDeadlineEnforcementResult(0, 1)
                : await _deadlineService.EnforceCustomerActionsWithinTransactionAsync(userId);
            if (enforcement.ExpiredCount > 0)
            {
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;
                throw new ConcurrencyConflictException("مهلت تکمیل احراز هویت این خرید پایان یافته است و باید توسط مدیریت بررسی شود.");
            }

            var now = DateTime.UtcNow;

            var profile = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .FirstOrDefaultAsync(x => x.UserId == userId);

            await EnsureRequiredDocumentsUploadedAsync(userId, profile);

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
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"verification:profile:{profile.Id:N}");

            var protectedData = new ProtectedVerificationData(
                request.FirstName.Trim(), request.LastName.Trim(), nationalCode,
                request.BirthDate, request.BankCardNumber?.Trim(), request.ShabaNumber?.Trim(),
                request.Address?.Trim(), request.PostalCode?.Trim(),
                request.RegisteredMobileBelongsToCardHolder, cardHolderMobile);
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

            var transitioned = _lifecycleCoordinator is null || profile.Id == Guid.Empty
                ? 0
                : await _lifecycleCoordinator.SynchronizeSubmissionAsync(userId, profile.Id);

            if (transitioned > 0 || profile.VerificationDocuments.Count == 0)
                await _notificationService.CreateAsync(
                    userId,
                    (byte)NotificationType.VerificationSubmitted,
                    "درخواست احراز هویت ثبت شد",
                    "درخواست احراز هویت شما ثبت شد و در انتظار بررسی ادمین است.");

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            committed = true;

            _logger.LogInformation(
                "KYC profile submitted. UserId={UserId} ProfileId={ProfileId} DocumentCount={DocumentCount} EventType={EventType}",
                userId, profile.Id, profile.VerificationDocuments.Count, OperationalEventNames.KycUploaded);

            return MapProfile(profile);
            }
            catch
            {
                if (!committed) await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<VerificationDocumentDto> AddDocumentAsync(
            Guid userId,
            byte documentType,
            string filePath,
            Guid? kycDocumentTypeId = null,
            Guid? orderItemId = null,
            bool isRedacted = false)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (string.IsNullOrWhiteSpace(filePath))
                throw new BusinessException("مسیر فایل معتبر نیست.");
            var expectedPrefix = $"kyc-private:{userId:N}/";
            if (!filePath.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
                filePath.Contains("..", StringComparison.Ordinal) || filePath.Length > 500)
                throw new BusinessException("توکن فایل احراز هویت معتبر نیست.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"verification:user:{userId:N}");
            var committed = false;
            try
            {
            var profile = await _dbContext.UserVerificationProfiles
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (profile == null)
            {
                // Documents may be uploaded before the textual form is submitted.
                // A draft never advances order KYC and cannot be reviewed by staff.
                profile = new UserVerificationProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    NationalCode = string.Empty,
                    Status = (byte)VerificationStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.UserVerificationProfiles.AddAsync(profile);
            }
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"verification:profile:{profile.Id:N}");

            if (profile.Status == (byte)VerificationStatus.Verified)
                throw new BusinessException("پرونده تأیید شده است و مدرک جدید نمی‌توان ثبت کرد.");

            if (orderItemId.HasValue && _deadlineService is not null)
            {
                var enforcement = await _deadlineService.EnforceCustomerActionsWithinTransactionAsync(userId, orderItemId);
                if (enforcement.ExpiredCount > 0)
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    committed = true;
                    throw new ConcurrencyConflictException("مهلت تکمیل احراز هویت این خرید پایان یافته است و امکان بارگذاری مدرک وجود ندارد.");
                }
            }

            var now = DateTime.UtcNow;
            // A versioned document must be bound to the exact paid order item
            // selected by the Customer. Looking up only the document type would
            // let a caller reuse a valid type from an unrelated policy/version.
            if (kycDocumentTypeId.HasValue)
            {
                if (!orderItemId.HasValue)
                    throw new BusinessException("آیتم سفارش برای مدرک سیاست احراز هویت الزامی است.");

                var requirement = await _dbContext.OrderItems
                    .Where(x => x.Id == orderItemId.Value && x.Order.UserId == userId && x.KycPolicyVersionId != null)
                    .Join(_dbContext.KycPolicyDocumentRequirements,
                        item => item.KycPolicyVersionId,
                        policyRequirement => (Guid?)policyRequirement.KycPolicyVersionId,
                        (item, policyRequirement) => policyRequirement)
                    .FirstOrDefaultAsync(x => x.KycDocumentTypeId == kycDocumentTypeId.Value);
                if (requirement is null)
                    throw new BusinessException("این نوع مدرک در سیاست آیتم سفارش انتخاب‌شده وجود ندارد.");

                // A required redaction is part of the immutable policy version
                // captured on that paid order item. Normal uploads cannot
                // satisfy that slot through the Customer UI.
                if (requirement.RedactionMode == (byte)KycDocumentRedactionMode.Required && !isRedacted)
                    throw new BusinessException("این مدرک باید از طریق ابزار پوشاندن اطلاعات ارسال شود.");
            }

            var document = new VerificationDocument
            {
                Id = Guid.NewGuid(),
                UserVerificationProfileId = profile.Id,
                DocumentType = documentType,
                KycDocumentTypeId = kycDocumentTypeId,
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

            // A draft accepts images first, but it must not move the paid item
            // to review until the customer has submitted every required text
            // field. SubmitAsync performs that final transition.
            if (_lifecycleCoordinator is not null && profile.SubmittedAt.HasValue &&
                !string.IsNullOrWhiteSpace(profile.EncryptedPayload))
            {
                await _lifecycleCoordinator.SynchronizeSubmissionAsync(userId, profile.Id);
                await _dbContext.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            committed = true;

            _logger.LogInformation(
                "KYC document registered. UserId={UserId} ProfileId={ProfileId} FileId={FileId} DocumentType={DocumentType} EventType={EventType}",
                userId, profile.Id, document.Id, documentType, OperationalEventNames.KycUploaded);

            return MapDocument(document);
            }
            catch
            {
                if (!committed) await transaction.RollbackAsync();
                throw;
            }
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

            if (document.Status is not ((byte)VerificationStatus.Pending or (byte)VerificationStatus.Rejected))
                throw new BusinessException("فقط مدارک در انتظار بررسی یا ردشده قابل حذف هستند.");

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

        public async Task<Vitorize.Shared.Common.PagedResult<VerificationProfileDto>> GetPagedAsync(AdminVerificationFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminVerificationFilterDto();
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.UserVerificationProfiles.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                if (search.Length > 250) search = search[..250];
                query = query.Where(x => x.NationalCode.Contains(search) || x.User.FullName.Contains(search) || x.User.Mobile.Contains(search));
            }
            if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("status", "asc") => query.OrderBy(x => x.Status).ThenBy(x => x.Id),
                ("status", "desc") => query.OrderByDescending(x => x.Status).ThenBy(x => x.Id),
                ("submittedat", "asc") => query.OrderBy(x => x.SubmittedAt ?? x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt).ThenBy(x => x.Id)
            };
            var profiles = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Include(x => x.VerificationDocuments).ToListAsync(cancellationToken);
            return new Vitorize.Shared.Common.PagedResult<VerificationProfileDto>
            {
                Items = profiles.Select(MapProfile).ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount
            };
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
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"verification:profile:{profileId:N}");
            try
            {
            var profile = await _dbContext.UserVerificationProfiles
                .Include(x => x.VerificationDocuments)
                .FirstOrDefaultAsync(x => x.Id == profileId);

            if (profile == null)
                throw new NotFoundException("پرونده احراز هویت یافت نشد.");

            if (!profile.SubmittedAt.HasValue || string.IsNullOrWhiteSpace(profile.EncryptedPayload))
                throw new BusinessException("کاربر هنوز اطلاعات و مدارک احراز هویت را به‌طور کامل ثبت نکرده است.");

            var requestedStatus = request.Approve
                ? (byte)VerificationStatus.Verified
                : (byte)VerificationStatus.Rejected;
            if (profile.Status != (byte)VerificationStatus.Pending)
            {
                if (profile.Status == requestedStatus)
                {
                    await transaction.CommitAsync();
                    return MapProfile(profile);
                }
                throw new ConcurrencyConflictException("وضعیت احراز هویت در همین فاصله تغییر کرده است. اطلاعات را تازه‌سازی کنید.");
            }

            // A manager must not approve an order-total verification before the
            // policy's required images are actually present. Identity details
            // are saved before document upload, so this guard is intentionally
            // here (rather than in SubmitAsync) to preserve that two-step UI.
            if (request.Approve)
            {
                var policyIds = await _dbContext.OrderItems.AsNoTracking()
                    .Where(x => x.Order.UserId == profile.UserId &&
                                x.Order.PaymentStatus == (byte)PaymentStatus.Paid &&
                                x.RequiresVerification && x.KycPolicyVersionId.HasValue)
                    .Select(x => x.KycPolicyVersionId!.Value)
                    .Distinct()
                    .ToListAsync();

                if (policyIds.Count > 0)
                {
                    var requiredDocumentIds = await _dbContext.KycPolicyDocumentRequirements.AsNoTracking()
                        .Where(x => policyIds.Contains(x.KycPolicyVersionId) && x.IsRequired)
                        .Select(x => x.KycDocumentTypeId)
                        .Distinct()
                        .ToListAsync();
                    var uploadedDocumentIds = profile.VerificationDocuments
                        .Where(x => x.Status == (byte)VerificationStatus.Pending && x.KycDocumentTypeId.HasValue)
                        .Select(x => x.KycDocumentTypeId!.Value)
                        .ToHashSet();

                    if (!requiredDocumentIds.All(uploadedDocumentIds.Contains))
                        throw new BusinessException("پیش از تأیید احراز هویت، همه مدارک الزامی سفارش باید بارگذاری شوند.");
                }
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == profile.UserId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var now = DateTime.UtcNow;
            var status = requestedStatus;

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

            if (_lifecycleCoordinator is not null)
                await _lifecycleCoordinator.SynchronizeReviewAsync(user.Id, profile.Id, request.Approve);

            if (request.Approve)
            {
                await _smsOutbox.EnqueueTextAsync(
                    user.Mobile,
                    "ویتورایز\nاحراز هویت شما با موفقیت تایید شد.",
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
                await _smsOutbox.EnqueueTextAsync(
                    user.Mobile,
                    string.IsNullOrWhiteSpace(request.AdminNote)
                        ? "ویتورایز\nدرخواست احراز هویت شما رد شد."
                        : $"ویتورایز\nدرخواست احراز هویت شما رد شد. علت: {request.AdminNote.Trim()}",
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
            await transaction.CommitAsync();

            _logger.LogInformation(
                "KYC review completed. UserId={UserId} ProfileId={ProfileId} AdminUserId={AdminUserId} DocumentCount={DocumentCount} Approved={Approved} EventType={EventType}",
                user.Id, profile.Id, adminUserId, profile.VerificationDocuments.Count, request.Approve,
                request.Approve ? OperationalEventNames.KycApproved : OperationalEventNames.KycRejected);

            // Fulfillment is intentionally after the durable verification
            // commit. A release failure remains operationally retryable and
            // must never turn a completed approval into a failed KYC decision.
            if (request.Approve && _fulfillmentReleaseService is not null)
                await _fulfillmentReleaseService.ReleaseSatisfiedItemsForVerificationAsync(profile.Id);

            return MapProfile(profile);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
                RegisteredMobileBelongsToCardHolder = data.RegisteredMobileBelongsToCardHolder,
                CardHolderMobile = data.CardHolderMobile,
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
                profile.ShabaNumber, profile.Address, profile.PostalCode, null, null);
        }

        private static VerificationDocumentDto MapDocument(VerificationDocument document)
        {
            return new VerificationDocumentDto
            {
                Id = document.Id,
                DocumentType = document.DocumentType,
                KycDocumentTypeId = document.KycDocumentTypeId,
                FilePath = $"/api/verification/documents/{document.Id}/content",
                Status = document.Status,
                AdminNote = document.AdminNote
            };
        }

        private async Task EnsureRequiredDocumentsUploadedAsync(Guid userId, UserVerificationProfile? profile)
        {
            var pendingDocuments = profile?.VerificationDocuments
                .Where(document => document.Status == (byte)VerificationStatus.Pending)
                .ToList() ?? [];

            var requiredDocumentTypeIds = await _dbContext.OrderItems.AsNoTracking()
                .Where(item => item.Order.UserId == userId &&
                               item.Order.PaymentStatus == (byte)PaymentStatus.Paid &&
                               item.RequiresVerification &&
                               item.KycPolicyVersionId.HasValue)
                .Join(_dbContext.KycPolicyDocumentRequirements,
                    item => item.KycPolicyVersionId,
                    requirement => (Guid?)requirement.KycPolicyVersionId,
                    (_, requirement) => requirement)
                .Where(requirement => requirement.IsRequired)
                .Select(requirement => requirement.KycDocumentTypeId)
                .Distinct()
                .ToListAsync();

            var complete = requiredDocumentTypeIds.Count > 0
                ? requiredDocumentTypeIds.All(requiredId => pendingDocuments.Any(document => document.KycDocumentTypeId == requiredId))
                : new byte[] { 1, 4 }.All(requiredType => pendingDocuments.Any(document =>
                    document.KycDocumentTypeId is null && document.DocumentType == requiredType));

            if (!complete)
                throw new BusinessException("پیش از ثبت احراز هویت، همه مدارک تصویری الزامی را بارگذاری کنید.");
        }

        private static string NormalizeNationalCode(string value) =>
            string.Concat(value.Trim().Select(static character => character switch
            {
                '۰' => '0', '۱' => '1', '۲' => '2', '۳' => '3', '۴' => '4',
                '۵' => '5', '۶' => '6', '۷' => '7', '۸' => '8', '۹' => '9',
                '٠' => '0', '١' => '1', '٢' => '2', '٣' => '3', '٤' => '4',
                '٥' => '5', '٦' => '6', '٧' => '7', '٨' => '8', '٩' => '9',
                _ => character
            }));

        private sealed record ProtectedVerificationData(
            string FirstName, string LastName, string NationalCode, DateOnly? BirthDate,
            string? BankCardNumber, string? ShabaNumber, string? Address, string? PostalCode,
            bool? RegisteredMobileBelongsToCardHolder = null, string? CardHolderMobile = null);
    }
}
