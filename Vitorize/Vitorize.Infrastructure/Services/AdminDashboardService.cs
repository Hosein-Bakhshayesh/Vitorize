using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Dashboard;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly VitorizeDbContext _context;

        public AdminDashboardService(VitorizeDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardDto> GetDashboardAsync()
        {
            var totalUsers = await _context.Users
                .CountAsync(x => !x.IsDeleted);

            var totalProducts = await _context.Products
                .CountAsync(x => x.IsActive);

            var totalOrders = await _context.Orders.CountAsync();

            var pendingOrders = await _context.Orders
                .CountAsync(x => x.Status == 1);

            var completedOrders = await _context.Orders
                .CountAsync(x => x.Status == 3);

            var totalRevenue = await _context.Orders
                .Where(x => x.PaymentStatus == 2)
                .SumAsync(x => (decimal?)x.FinalAmount) ?? 0;

            var activeCoupons = await _context.Coupons
                .CountAsync(x => x.IsActive);

            var availableGiftCodes = await _context.GiftCodes
                .CountAsync(x => x.Status == 0);

            var recentOrders = await _context.Orders
                .Include(x => x.User)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new DashboardRecentOrderDto
                {
                    Id = x.Id,
                    OrderNumber = x.OrderNumber,
                    CustomerName = x.User.FullName,
                    FinalAmount = x.FinalAmount,
                    Status = x.Status,
                    PaymentStatus = x.PaymentStatus,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return new AdminDashboardDto
            {
                TotalUsers = totalUsers,
                TotalProducts = totalProducts,
                TotalOrders = totalOrders,
                PendingOrders = pendingOrders,
                CompletedOrders = completedOrders,
                TotalRevenue = totalRevenue,
                ActiveCoupons = activeCoupons,
                AvailableGiftCodes = availableGiftCodes,
                RecentOrders = recentOrders
            };
        }
    }
}