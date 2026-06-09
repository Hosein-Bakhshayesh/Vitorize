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

        public AdminDashboardService(
            VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            var today = DateTime.UtcNow.Date;

            var monthStart =
                new DateTime(
                    DateTime.UtcNow.Year,
                    DateTime.UtcNow.Month,
                    1);

            var summary = new DashboardSummaryDto
            {
                TotalUsers =
                    await _dbContext.Users.CountAsync(),

                NewUsersToday =
                    await _dbContext.Users
                        .CountAsync(x =>
                            x.CreatedAt >= today),

                TotalOrders =
                    await _dbContext.Orders.CountAsync(),

                OrdersToday =
                    await _dbContext.Orders
                        .CountAsync(x =>
                            x.CreatedAt >= today),

                RevenueToday =
                    await _dbContext.Orders
                        .Where(x =>
                            x.PaidAt != null &&
                            x.PaidAt >= today)
                        .SumAsync(x =>
                            (decimal?)x.FinalAmount)
                        ?? 0,

                RevenueThisMonth =
                    await _dbContext.Orders
                        .Where(x =>
                            x.PaidAt != null &&
                            x.PaidAt >= monthStart)
                        .SumAsync(x =>
                            (decimal?)x.FinalAmount)
                        ?? 0,

                PendingTickets =
                    await _dbContext.Tickets
                        .CountAsync(x =>
                            x.Status != (byte)TicketStatus.Closed),

                PendingVerifications =
                    await _dbContext.Users
                        .CountAsync(x =>
                            x.VerificationStatus ==
                            (byte)VerificationStatus.Pending),

                UnreadNotifications =
                    await _dbContext.Notifications
                        .CountAsync(x =>
                            !x.IsRead),

                AvailableGiftCodes =
                    await _dbContext.GiftCodes
                        .CountAsync(x =>
                            x.Status ==
                            (byte)GiftCodeStatus.Available),
                            };

            var topProducts =
                await _dbContext.OrderItems
                    .GroupBy(x => new
                    {
                        x.ProductId,
                        x.ProductTitle
                    })
                    .Select(x => new TopProductDto
                    {
                        ProductId = x.Key.ProductId,
                        ProductTitle = x.Key.ProductTitle,

                        TotalSold =
                            x.Sum(i => i.Quantity),

                        Revenue =
                            x.Sum(i => i.TotalPrice)
                    })
                    .OrderByDescending(x => x.TotalSold)
                    .Take(10)
                    .ToListAsync();

            return new DashboardDto
            {
                Summary = summary,
                TopProducts = topProducts
            };
        }
    }
}