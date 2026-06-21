using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Reports;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services
{
    public class AdminReportService : IAdminReportService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminReportService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<SalesReportDto> GetSalesReportAsync(ReportDateRangeDto filter)
        {
            var (from, to) = NormalizeRange(filter);

            var paidOrders = _dbContext.Orders
                .AsNoTracking()
                .Where(x =>
                    x.PaymentStatus == (byte)PaymentStatus.Paid &&
                    x.PaidAt >= from &&
                    x.PaidAt < to);

            var totalRevenue = await paidOrders.SumAsync(x => (decimal?)x.FinalAmount) ?? 0;
            var paidOrdersCount = await paidOrders.CountAsync();

            var dailyRaw = await paidOrders
                .GroupBy(x => x.PaidAt!.Value.Date)
                .Select(x => new
                {
                    Date = x.Key,
                    OrdersCount = x.Count(),
                    Revenue = x.Sum(o => o.FinalAmount)
                })
                .ToListAsync();

            var topProducts = await _dbContext.OrderItems
                .AsNoTracking()
                .Where(x =>
                    x.Order.PaymentStatus == (byte)PaymentStatus.Paid &&
                    x.Order.PaidAt >= from &&
                    x.Order.PaidAt < to)
                .GroupBy(x => new
                {
                    x.ProductId,
                    x.ProductTitle
                })
                .Select(x => new SalesReportProductDto
                {
                    ProductId = x.Key.ProductId,
                    ProductTitle = x.Key.ProductTitle,
                    QuantitySold = x.Sum(i => i.Quantity),
                    Revenue = x.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToListAsync();

            return new SalesReportDto
            {
                TotalRevenue = totalRevenue,
                TotalOrders = await _dbContext.Orders
                    .AsNoTracking()
                    .CountAsync(x => x.CreatedAt >= from && x.CreatedAt < to),
                PaidOrders = paidOrdersCount,
                AverageOrderValue = paidOrdersCount == 0 ? 0 : totalRevenue / paidOrdersCount,
                DailySales = dailyRaw
                    .OrderBy(x => x.Date)
                    .Select(x => new SalesReportDailyDto
                    {
                        Date = x.Date,
                        OrdersCount = x.OrdersCount,
                        Revenue = x.Revenue
                    })
                    .ToList(),
                TopProducts = topProducts
            };
        }

        public async Task<PaymentsReportDto> GetPaymentsReportAsync(ReportDateRangeDto filter)
        {
            var (from, to) = NormalizeRange(filter);

            var payments = _dbContext.Payments
                .AsNoTracking()
                .Where(x => x.RequestedAt >= from && x.RequestedAt < to);

            return new PaymentsReportDto
            {
                TotalPayments = await payments.CountAsync(),

                PaidPayments = await payments.CountAsync(x =>
                    x.Status == (byte)PaymentStatus.Paid),

                FailedPayments = await payments.CountAsync(x =>
                    x.Status == (byte)PaymentStatus.Failed ||
                    x.Status == (byte)PaymentStatus.Cancelled),

                PaidAmount = await payments
                    .Where(x => x.Status == (byte)PaymentStatus.Paid)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0,

                ByGateway = await payments
                    .GroupBy(x => x.Gateway)
                    .Select(x => new PaymentGatewayReportDto
                    {
                        Gateway = x.Key,
                        Count = x.Count(),
                        Amount = x.Sum(p => p.Amount)
                    })
                    .ToListAsync(),

                ByStatus = await payments
                    .GroupBy(x => x.Status)
                    .Select(x => new PaymentStatusReportDto
                    {
                        Status = x.Key,
                        Count = x.Count(),
                        Amount = x.Sum(p => p.Amount)
                    })
                    .ToListAsync()
            };
        }

        public async Task<WalletReportDto> GetWalletReportAsync(ReportDateRangeDto filter)
        {
            var (from, to) = NormalizeRange(filter);

            var tx = _dbContext.WalletTransactions
                .AsNoTracking()
                .Where(x => x.CreatedAt >= from && x.CreatedAt < to);

            return new WalletReportDto
            {
                TransactionsCount = await tx.CountAsync(),

                TotalCredit = await tx
                    .Where(x => x.Type == (byte)WalletTransactionType.Credit)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0,

                TotalDebit = await tx
                    .Where(x => x.Type == (byte)WalletTransactionType.Debit)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0,

                ByType = await tx
                    .GroupBy(x => x.Type)
                    .Select(x => new WalletTransactionTypeReportDto
                    {
                        Type = x.Key,
                        Count = x.Count(),
                        Amount = x.Sum(t => t.Amount)
                    })
                    .ToListAsync()
            };
        }

        public async Task<CouponsReportDto> GetCouponsReportAsync(ReportDateRangeDto filter)
        {
            var (from, to) = NormalizeRange(filter);

            return new CouponsReportDto
            {
                TotalCoupons = await _dbContext.Coupons
                    .AsNoTracking()
                    .CountAsync(),

                ActiveCoupons = await _dbContext.Coupons
                    .AsNoTracking()
                    .CountAsync(x => x.IsActive),

                TotalUsages = await _dbContext.CouponUsages
                    .AsNoTracking()
                    .CountAsync(x => x.UsedAt >= from && x.UsedAt < to),

                TopCoupons = await _dbContext.CouponUsages
                    .AsNoTracking()
                    .Where(x => x.UsedAt >= from && x.UsedAt < to)
                    .GroupBy(x => new
                    {
                        x.CouponId,
                        x.Coupon.Code,
                        x.Coupon.Title
                    })
                    .Select(x => new CouponUsageReportDto
                    {
                        CouponId = x.Key.CouponId,
                        Code = x.Key.Code,
                        Title = x.Key.Title,
                        UsageCount = x.Count()
                    })
                    .OrderByDescending(x => x.UsageCount)
                    .Take(10)
                    .ToListAsync()
            };
        }

        public async Task<GiftCodesReportDto> GetGiftCodesReportAsync()
        {
            return new GiftCodesReportDto
            {
                TotalCodes = await _dbContext.GiftCodes
                    .AsNoTracking()
                    .CountAsync(),

                ByStatus = await _dbContext.GiftCodes
                    .AsNoTracking()
                    .GroupBy(x => x.Status)
                    .Select(x => new GiftCodeStatusReportDto
                    {
                        Status = x.Key,
                        Count = x.Count()
                    })
                    .ToListAsync(),

                ByProduct = await _dbContext.GiftCodes
                    .AsNoTracking()
                    .GroupBy(x => new
                    {
                        x.ProductId,
                        x.Product.Title
                    })
                    .Select(x => new GiftCodeProductReportDto
                    {
                        ProductId = x.Key.ProductId,
                        ProductTitle = x.Key.Title,
                        TotalCodes = x.Count(),
                        AvailableCodes = x.Count(c => c.Status == (byte)GiftCodeStatus.Available),
                        SoldCodes = x.Count(c =>
                            c.Status == (byte)GiftCodeStatus.Sold ||
                            c.Status == (byte)GiftCodeStatus.Delivered)
                    })
                    .OrderByDescending(x => x.TotalCodes)
                    .Take(20)
                    .ToListAsync()
            };
        }

        public async Task<UsersReportDto> GetUsersReportAsync(ReportDateRangeDto filter)
        {
            var (from, to) = NormalizeRange(filter);

            var dailyRaw = await _dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    !x.IsDeleted &&
                    x.CreatedAt >= from &&
                    x.CreatedAt < to)
                .GroupBy(x => x.CreatedAt.Date)
                .Select(x => new
                {
                    Date = x.Key,
                    Count = x.Count()
                })
                .ToListAsync();

            return new UsersReportDto
            {
                TotalUsers = await _dbContext.Users
                    .AsNoTracking()
                    .CountAsync(x => !x.IsDeleted),

                NewUsers = await _dbContext.Users
                    .AsNoTracking()
                    .CountAsync(x =>
                        !x.IsDeleted &&
                        x.CreatedAt >= from &&
                        x.CreatedAt < to),

                ActiveUsers = await _dbContext.Users
                    .AsNoTracking()
                    .CountAsync(x =>
                        !x.IsDeleted &&
                        x.Status == (byte)UserStatus.Active),

                BlockedUsers = await _dbContext.Users
                    .AsNoTracking()
                    .CountAsync(x =>
                        !x.IsDeleted &&
                        x.Status == (byte)UserStatus.Blocked),

                VerifiedUsers = await _dbContext.Users
                    .AsNoTracking()
                    .CountAsync(x =>
                        !x.IsDeleted &&
                        x.VerificationStatus == (byte)VerificationStatus.Verified),

                DailyRegistrations = dailyRaw
                    .OrderBy(x => x.Date)
                    .Select(x => new UserRegistrationDailyDto
                    {
                        Date = x.Date,
                        Count = x.Count
                    })
                    .ToList()
            };
        }

        private static (DateTime From, DateTime To) NormalizeRange(ReportDateRangeDto filter)
        {
            var to = filter.To?.Date.AddDays(1) ?? DateTime.UtcNow.Date.AddDays(1);
            var from = filter.From?.Date ?? to.AddDays(-30);

            return (from, to);
        }
    }
}