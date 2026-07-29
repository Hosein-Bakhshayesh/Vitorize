using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Orders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            VitorizeDbContext dbContext,
            INotificationService notificationService,
            IEncryptionService encryptionService,
            ILogger<OrderService>? logger = null)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _encryptionService = encryptionService;
            _logger = logger ?? NullLogger<OrderService>.Instance;
        }

        public async Task<List<OrderDto>> GetMyOrdersAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return orders.Select(MapOrderSummary).ToList();
        }

        public async Task<OrderDto> GetMyOrderDetailsAsync(Guid userId, Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.InputValues)
                .FirstOrDefaultAsync(x =>
                    x.Id == orderId &&
                    x.UserId == userId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            return MapOrderDetails(order);
        }

        public async Task<List<DeliveredCodeDto>> GetMyDeliveredCodesAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var rows = await _dbContext.OrderItemDeliveries
                .AsNoTracking()
                .Where(x =>
                    x.IsVisibleToCustomer &&
                    x.OrderItem.Order.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new DeliveredCodeDto
                {
                    Id = x.Id,
                    OrderId = x.OrderItem.OrderId,
                    OrderNumber = x.OrderItem.Order.OrderNumber,
                    OrderItemId = x.OrderItemId,
                    ProductId = x.OrderItem.ProductId,
                    ProductTitle = x.OrderItem.ProductTitle,
                    VariantTitle = x.OrderItem.VariantTitle,
                    DeliveryType = x.DeliveryType,
                    DeliveredContent = x.DeliveredContent,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            var versions = await _dbContext.OrderItemDeliveries.AsNoTracking()
                .Where(x => x.IsVisibleToCustomer && x.OrderItem.Order.UserId == userId)
                .ToDictionaryAsync(x => x.Id, x => x.EncryptionVersion);
            foreach (var row in rows)
                row.DeliveredContent = UnprotectDelivery(row.DeliveredContent, versions.GetValueOrDefault(row.Id));
            return rows;
        }

        public async Task<List<OrderDto>> GetAdminOrdersAsync()
        {
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return orders.Select(MapOrderSummary).ToList();
        }

        public async Task<OrderDto> GetAdminOrderDetailsAsync(Guid orderId)
        {
            var order = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.InputValues)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            return MapOrderDetails(order);
        }

        public async Task<List<OrderDto>> SearchAdminOrdersAsync(AdminOrderFilterDto filter)
        {
            filter ??= new AdminOrderFilterDto();

            var query = _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.OrderNumber))
            {
                var orderNumber = filter.OrderNumber.Trim();

                query = query.Where(x =>
                    x.OrderNumber.Contains(orderNumber));
            }

            if (filter.UserId.HasValue)
            {
                query = query.Where(x =>
                    x.UserId == filter.UserId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == filter.Status.Value);
            }

            if (filter.PaymentStatus.HasValue)
            {
                query = query.Where(x =>
                    x.PaymentStatus == filter.PaymentStatus.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt <= filter.ToDate.Value);
            }

            var orders = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return orders.Select(MapOrderSummary).ToList();
        }

        public async Task<Vitorize.Shared.Common.PagedResult<OrderDto>> GetPagedAdminOrdersAsync(AdminOrderFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminOrderFilterDto();
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.Orders.AsNoTracking().AsQueryable();
            var search = string.IsNullOrWhiteSpace(filter.Search) ? filter.OrderNumber : filter.Search;
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                if (search.Length > 250) search = search[..250];
                query = query.Where(x => x.OrderNumber.Contains(search) || x.User.FullName.Contains(search) || x.User.Mobile.Contains(search));
            }
            if (filter.UserId.HasValue) query = query.Where(x => x.UserId == filter.UserId.Value);
            if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
            if (filter.PaymentStatus.HasValue) query = query.Where(x => x.PaymentStatus == filter.PaymentStatus.Value);
            if (filter.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= filter.FromDate.Value);
            if (filter.ToDate.HasValue) query = query.Where(x => x.CreatedAt < filter.ToDate.Value.AddDays(1));
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("amount", "asc") => query.OrderBy(x => x.FinalAmount).ThenBy(x => x.Id),
                ("amount", "desc") => query.OrderByDescending(x => x.FinalAmount).ThenBy(x => x.Id),
                ("status", "asc") => query.OrderBy(x => x.Status).ThenBy(x => x.Id),
                ("status", "desc") => query.OrderByDescending(x => x.Status).ThenBy(x => x.Id),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            };
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new OrderDto
            {
                Id = x.Id, OrderNumber = x.OrderNumber, UserFullName = x.User.FullName, UserMobile = x.User.Mobile,
                Status = x.Status, PaymentStatus = x.PaymentStatus, SubtotalAmount = x.SubtotalAmount,
                DiscountAmount = x.DiscountAmount, FinalAmount = x.FinalAmount, CurrencyType = x.CurrencyType,
                CreatedAt = x.CreatedAt, PaidAt = x.PaidAt, CompletedAt = x.CompletedAt
            }).ToListAsync(cancellationToken);
            return new Vitorize.Shared.Common.PagedResult<OrderDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<List<OrderDto>> GetSelectedAdminOrdersForExportAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        {
            var selected = ids.Where(x => x != Guid.Empty).Distinct().ToArray();
            if (selected.Length == 0) throw new BusinessException("حداقل یک سفارش باید انتخاب شود.");
            if (selected.Length > 200) throw new BusinessException("حداکثر ۲۰۰ سفارش را می‌توان هم‌زمان خروجی گرفت.");
            if (selected.Length != ids.Count) throw new BusinessException("شناسه‌های انتخاب‌شده معتبر نیستند.");
            var query = _dbContext.Orders.AsNoTracking().Where(x => selected.Contains(x.Id));
            if (await query.CountAsync(cancellationToken) != selected.Length)
                throw new BusinessException("یکی از سفارش‌های انتخاب‌شده معتبر نیست.");
            return await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new OrderDto
            {
                Id = x.Id, OrderNumber = x.OrderNumber, UserFullName = x.User.FullName, UserMobile = x.User.Mobile,
                Status = x.Status, PaymentStatus = x.PaymentStatus, FinalAmount = x.FinalAmount, CurrencyType = x.CurrencyType, CreatedAt = x.CreatedAt
            }).ToListAsync(cancellationToken);
        }

        public async Task CancelOrderAsync(
            Guid orderId,
            Guid adminUserId,
            string? reason)
        {
            if (orderId == Guid.Empty)
                throw new BusinessException("شناسه سفارش معتبر نیست.");

            if (adminUserId == Guid.Empty)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .Include(x => x.GiftCodeReservations)
                    .ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.Status == (byte)OrderStatus.Completed)
                throw new BusinessException("سفارش تکمیل شده قابل لغو نیست.");

            if (order.Status == (byte)OrderStatus.Cancelled)
                throw new BusinessException("این سفارش قبلاً لغو شده است.");

            // A paid order cannot take the ordinary cancellation path because
            // that would leave captured customer funds without a refund record.
            // Finance staff must use the idempotent refund workflow instead.
            if (order.PaymentStatus == (byte)PaymentStatus.Paid)
                throw new BusinessException("سفارش پرداخت‌شده باید ابتدا از مسیر بازپرداخت مالی لغو شود.");

            var hasDelivery = order.OrderItems
                .Any(x => x.OrderItemDeliveries.Any());

            if (hasDelivery)
                throw new BusinessException("سفارشی که کد آن تحویل شده قابل لغو نیست.");

            var now = DateTime.UtcNow;
            var fromStatus = order.Status;

            foreach (var reservation in order.GiftCodeReservations)
            {
                if (reservation.Status == (byte)GiftCodeReservationStatus.Active)
                {
                    reservation.Status = (byte)GiftCodeReservationStatus.Released;
                    reservation.ReleasedAt = now;

                    reservation.GiftCode.Status = (byte)GiftCodeStatus.Available;
                    reservation.GiftCode.ReservedByUserId = null;
                    reservation.GiftCode.ReservedAt = null;
                    reservation.GiftCode.ReservationExpiresAt = null;
                    reservation.GiftCode.UpdatedAt = now;
                }
            }

            order.Status = (byte)OrderStatus.Cancelled;
            order.AdminNote = reason;
            order.UpdatedAt = now;

            await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                FromStatus = fromStatus,
                ToStatus = order.Status,
                ChangedByUserId = adminUserId,
                Note = string.IsNullOrWhiteSpace(reason)
                    ? "سفارش توسط ادمین لغو شد."
                    : reason.Trim(),
                CreatedAt = now
            });

            await _notificationService.CreateAsync(
                order.UserId,
                (byte)NotificationType.OrderCancelled,
                "سفارش لغو شد",
                $"سفارش {order.OrderNumber} لغو شد. جزئیات در صفحه سفارش قابل مشاهده است.");

            await _dbContext.SaveChangesAsync();
        }

        public async Task CompleteOrderAsync(Guid orderId, Guid adminUserId)
        {
            if (orderId == Guid.Empty)
                throw new BusinessException("شناسه سفارش معتبر نیست.");

            if (adminUserId == Guid.Empty)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .Include(x => x.OrderItems)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.Status == (byte)OrderStatus.Completed)
                throw new BusinessException("این سفارش قبلاً تکمیل شده است.");

            if (order.Status == (byte)OrderStatus.Cancelled)
                throw new BusinessException("سفارش لغو شده قابل تکمیل نیست.");

            if (order.PaymentStatus != (byte)PaymentStatus.Paid)
                throw new BusinessException("سفارش پرداخت نشده قابل تکمیل نیست.");

            if (order.OrderItems.Any(x => x.DeliveryStatus != (byte)DeliveryStatus.Delivered))
                throw new BusinessException("تا زمان تحویل همه آیتم‌ها، تکمیل سفارش مجاز نیست.");

            var now = DateTime.UtcNow;
            var fromStatus = order.Status;

            order.Status = (byte)OrderStatus.Completed;
            order.CompletedAt ??= now;
            order.UpdatedAt = now;

            await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                FromStatus = fromStatus,
                ToStatus = order.Status,
                ChangedByUserId = adminUserId,
                Note = "سفارش توسط ادمین تکمیل شد.",
                CreatedAt = now
            });

            await _notificationService.CreateAsync(
                order.UserId,
                (byte)NotificationType.OrderCompleted,
                "سفارش تکمیل شد",
                $"سفارش {order.OrderNumber} تکمیل شد و جزئیات آن در حساب کاربری شما در دسترس است.");

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeliverManualAsync(
            Guid orderId,
            Guid adminUserId,
            ManualDeliveryRequestDto request)
        {
            if (orderId == Guid.Empty || request.OrderItemId == Guid.Empty)
                throw new BusinessException("شناسه سفارش یا آیتم معتبر نیست.");
            if (adminUserId == Guid.Empty)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");
            var content = request.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content) || content.Length > 4000)
                throw new BusinessException("محتوای تحویل باید بین ۱ تا ۴۰۰۰ نویسه باشد.");

            _logger.LogInformation(
                "Manual order delivery started. EventType={EventType} OrderId={OrderId} OrderItemId={OrderItemId}",
                "ManualDeliveryStarted", orderId, request.OrderItemId);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"manual-delivery:item:{request.OrderItemId:N}");

            var order = await _dbContext.Orders
                .Include(x => x.OrderItems).ThenInclude(x => x.OrderItemDeliveries)
                .FirstOrDefaultAsync(x => x.Id == orderId)
                ?? throw new NotFoundException("سفارش یافت نشد.");
            if (order.PaymentStatus != (byte)PaymentStatus.Paid ||
                order.Status is (byte)OrderStatus.Cancelled or (byte)OrderStatus.Refunded)
                throw new BusinessException("فقط سفارش پرداخت‌شده و فعال قابل تحویل است.");

            var item = order.OrderItems.FirstOrDefault(x => x.Id == request.OrderItemId)
                ?? throw new NotFoundException("آیتم سفارش یافت نشد.");
            if (item.DeliveryType != (byte)DeliveryType.Manual &&
                item.DeliveryType != (byte)DeliveryType.SupportRequired)
                throw new BusinessException("این آیتم برای تحویل دستی تعریف نشده است.");
            if (item.OrderItemDeliveries.Any(x =>
                    x.DeliveryType is (byte)DeliveryType.Manual or (byte)DeliveryType.SupportRequired))
                throw new BusinessException("این آیتم قبلاً تحویل شده است.");

            var now = DateTime.UtcNow;
            var delivery = new OrderItemDelivery
            {
                Id = Guid.NewGuid(),
                OrderItemId = item.Id,
                DeliveryType = item.DeliveryType,
                DeliveredContent = _encryptionService.Encrypt(content),
                ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
                EncryptionVersion = 2,
                ManualDeliveryItemKey = item.Id,
                IsVisibleToCustomer = request.IsVisibleToCustomer,
                DeliveredByUserId = adminUserId,
                CreatedAt = now
            };
            await _dbContext.OrderItemDeliveries.AddAsync(delivery);
            item.DeliveryStatus = (byte)DeliveryStatus.Delivered;
            item.DeliveredAt = now;

            var fromStatus = order.Status;
            if (order.OrderItems.All(x => x.Id == item.Id || x.DeliveryStatus == (byte)DeliveryStatus.Delivered))
            {
                order.Status = (byte)OrderStatus.Completed;
                order.CompletedAt ??= now;
            }
            order.UpdatedAt = now;

            await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(), OrderId = order.Id, FromStatus = fromStatus,
                ToStatus = order.Status, ChangedByUserId = adminUserId,
                Note = $"تحویل دستی آیتم {item.ProductTitle} ثبت شد.", CreatedAt = now
            });
            await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
            {
                EventType = "ManualDeliveryCompleted", EntityType = "OrderItemDelivery",
                EntityId = delivery.Id, UserId = adminUserId, CorrelationId = order.Id,
                Detail = $"order:{order.OrderNumber};item:{item.Id:N};encrypted:true",
                CreatedAt = now
            });
            await _notificationService.CreateAsync(order.UserId, (byte)NotificationType.GiftCodeDelivered,
                "تحویل سفارش", $"محتوای سفارش {order.OrderNumber} در حساب کاربری شما ثبت شد.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Manual order delivery completed. EventType={EventType} OrderId={OrderId} OrderItemId={OrderItemId} OrderNumber={OrderNumber} VisibleToCustomer={VisibleToCustomer}",
                "ManualDeliveryCompleted", order.Id, item.Id, order.OrderNumber, request.IsVisibleToCustomer);
        }

        private static OrderDto MapOrderSummary(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                SubtotalAmount = order.SubtotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                CurrencyType = order.CurrencyType,
                CreatedAt = order.CreatedAt,
                PaidAt = order.PaidAt,
                CompletedAt = order.CompletedAt,
                Items = order.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductVariantId = i.ProductVariantId,
                    ProductTitle = i.ProductTitle,
                    VariantTitle = i.VariantTitle,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    CurrencyType = i.CurrencyType,
                    DeliveryType = i.DeliveryType,
                    DeliveryStatus = i.DeliveryStatus,
                    RequiresVerification = i.RequiresVerification,
                    CreatedAt = i.CreatedAt,
                    DeliveredAt = i.DeliveredAt
                }).ToList()
            };
        }

        private OrderDto MapOrderDetails(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                SubtotalAmount = order.SubtotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                CurrencyType = order.CurrencyType,
                CreatedAt = order.CreatedAt,
                PaidAt = order.PaidAt,
                CompletedAt = order.CompletedAt,
                Items = order.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductVariantId = i.ProductVariantId,
                    ProductTitle = i.ProductTitle,
                    VariantTitle = i.VariantTitle,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    CurrencyType = i.CurrencyType,
                    DeliveryType = i.DeliveryType,
                    DeliveryStatus = i.DeliveryStatus,
                    RequiresVerification = i.RequiresVerification,
                    CreatedAt = i.CreatedAt,
                    DeliveredAt = i.DeliveredAt,
                    InputValues = i.InputValues.Select(MapInputValue).ToList(),
                    Deliveries = i.OrderItemDeliveries
                        .Where(d => d.IsVisibleToCustomer)
                        .Select(d => new OrderDeliveryDto
                        {
                            Id = d.Id,
                            OrderItemId = d.OrderItemId,
                            DeliveryType = d.DeliveryType,
                            GiftCodeId = d.GiftCodeId,
                            DeliveredContent = UnprotectDelivery(d.DeliveredContent, d.EncryptionVersion),
                            IsVisibleToCustomer = d.IsVisibleToCustomer,
                            CreatedAt = d.CreatedAt
                        })
                        .ToList()
                }).ToList()
            };
        }

        private static Vitorize.Application.DTOs.Products.ProductInputValueDto MapInputValue(OrderItemInputValue value) => new()
        {
            Id = value.Id,
            ProductInputFieldId = value.ProductInputFieldId,
            FieldKey = value.FieldKey,
            FieldLabel = value.FieldLabel,
            FieldType = value.FieldType,
            Value = value.IsSensitive ? ProductInputRules.Mask(null) : value.Value,
            IsSensitive = value.IsSensitive,
            IsMasked = value.IsSensitive
        };

        private string? UnprotectDelivery(string? value, short? encryptionVersion)
        {
            if (string.IsNullOrEmpty(value) || encryptionVersion is null or 0)
                return value;
            return _encryptionService.Decrypt(value);
        }
    }
}
