using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Dashboard;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminDashboardService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var last7DaysStart = today.AddDays(-6);

            var summary = new DashboardSummaryDto
            {
                TotalUsers = await _dbContext.Users
                    .CountAsync(x => !x.IsDeleted),

                NewUsersToday = await _dbContext.Users
                    .CountAsync(x =>
                        !x.IsDeleted &&
                        x.CreatedAt >= today &&
                        x.CreatedAt < tomorrow),

                TotalOrders = await _dbContext.Orders
                    .CountAsync(),

                OrdersToday = await _dbContext.Orders
                    .CountAsync(x =>
                        x.CreatedAt >= today &&
                        x.CreatedAt < tomorrow),

                RevenueToday = await _dbContext.Orders
                    .Where(x =>
                        x.PaidAt != null &&
                        x.PaidAt >= today &&
                        x.PaidAt < tomorrow)
                    .SumAsync(x => (decimal?)x.FinalAmount) ?? 0,

                RevenueThisMonth = await _dbContext.Orders
                    .Where(x =>
                        x.PaidAt != null &&
                        x.PaidAt >= monthStart)
                    .SumAsync(x => (decimal?)x.FinalAmount) ?? 0,

                TotalWalletBalance = await _dbContext.Wallets
                    .SumAsync(x => (decimal?)x.Balance) ?? 0,

                PendingTickets = await _dbContext.Tickets
                    .CountAsync(x =>
                        x.Status != (byte)TicketStatus.Closed),

                PendingVerifications = await _dbContext.UserVerificationProfiles
                    .CountAsync(x =>
                        x.Status == (byte)VerificationStatus.Pending),

                UnreadNotifications = await _dbContext.Notifications
                    .CountAsync(x => !x.IsRead),

                AvailableGiftCodes = await _dbContext.GiftCodes
                    .CountAsync(x =>
                        x.Status == (byte)GiftCodeStatus.Available),

                ReservedGiftCodes = await _dbContext.GiftCodes
                    .CountAsync(x =>
                        x.Status == (byte)GiftCodeStatus.Reserved),

                SoldGiftCodes = await _dbContext.GiftCodes
                    .CountAsync(x =>
                        x.Status == (byte)GiftCodeStatus.Sold ||
                        x.Status == (byte)GiftCodeStatus.Delivered)
            };

            var topProducts = await _dbContext.OrderItems
                .Where(x =>
                    x.Order.PaymentStatus == (byte)PaymentStatus.Paid)
                .GroupBy(x => new
                {
                    x.ProductId,
                    x.ProductTitle
                })
                .Select(x => new TopProductDto
                {
                    ProductId = x.Key.ProductId,
                    ProductTitle = x.Key.ProductTitle,
                    TotalSold = x.Sum(i => i.Quantity),
                    Revenue = x.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToListAsync();

            var salesRaw = await _dbContext.Orders
                .Where(x =>
                    x.PaidAt != null &&
                    x.PaidAt >= last7DaysStart)
                .GroupBy(x => x.PaidAt!.Value.Date)
                .Select(x => new
                {
                    Date = x.Key,
                    Value = x.Sum(i => i.FinalAmount)
                })
                .ToListAsync();

            var ordersRaw = await _dbContext.Orders
                .Where(x => x.CreatedAt >= last7DaysStart)
                .GroupBy(x => x.CreatedAt.Date)
                .Select(x => new
                {
                    Date = x.Key,
                    Count = x.Count()
                })
                .ToListAsync();

            var salesLast7Days = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date = last7DaysStart.AddDays(i);

                    return new DashboardChartPointDto
                    {
                        Date = date,
                        Value = salesRaw
                            .FirstOrDefault(x => x.Date == date)
                            ?.Value ?? 0
                    };
                })
                .ToList();

            var ordersLast7Days = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date = last7DaysStart.AddDays(i);

                    return new DashboardChartPointDto
                    {
                        Date = date,
                        Value = ordersRaw
                            .FirstOrDefault(x => x.Date == date)
                            ?.Count ?? 0
                    };
                })
                .ToList();

            var orderStatusCounts = await _dbContext.Orders
                .GroupBy(x => x.Status)
                .Select(x => new DashboardStatusCountDto
                {
                    Status = x.Key,
                    Count = x.Count()
                })
                .ToListAsync();

            var paymentStatusCounts = await _dbContext.Payments
                .GroupBy(x => x.Status)
                .Select(x => new DashboardStatusCountDto
                {
                    Status = x.Key,
                    Count = x.Count()
                })
                .ToListAsync();

            var giftCodeStatusCounts = await _dbContext.GiftCodes
                .GroupBy(x => x.Status)
                .Select(x => new DashboardStatusCountDto
                {
                    Status = x.Key,
                    Count = x.Count()
                })
                .ToListAsync();

            return new DashboardDto
            {
                Summary = summary,
                TopProducts = topProducts,
                SalesLast7Days = salesLast7Days,
                OrdersLast7Days = ordersLast7Days,
                OrderStatusCounts = orderStatusCounts,
                PaymentStatusCounts = paymentStatusCounts,
                GiftCodeStatusCounts = giftCodeStatusCounts
            };
        }
    }
}