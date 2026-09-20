using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Web.Models.Admin.Reports;
using Xunit;

namespace Vitorize.Tests;

public sealed class AdminMetricsContractTests
{
    [Fact]
    public void Report_models_deserialize_the_api_metric_names()
    {
        var json = """
        {
          "totalRevenue": 450000,
          "totalVatAmount": 41000,
          "totalOrders": 4,
          "paidOrders": 3,
          "dailySales": [{ "date": "2026-09-20T00:00:00Z", "ordersCount": 3, "revenue": 450000 }]
        }
        """;

        var report = JsonSerializer.Deserialize<SalesReportModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        report.Should().NotBeNull();
        report!.TotalRevenue.Should().Be(450000m);
        report.TotalVatAmount.Should().Be(41000m);
        report.PaidOrders.Should().Be(3);
        report.DailySales.Should().ContainSingle().Which.Revenue.Should().Be(450000m);
    }

    [Fact]
    public void All_remaining_report_models_deserialize_the_api_metric_names()
    {
        const string json = """
        {
          "payments": { "paidAmount": 90000, "totalPayments": 3, "paidPayments": 2, "failedPayments": 1, "byGateway": [{ "gateway": "Zarinpal", "count": 3, "amount": 90000 }] },
          "wallet": { "totalCredit": 40000, "totalDebit": 10000, "currentBalance": 70000, "transactionsCount": 2 },
          "coupons": { "totalCoupons": 4, "activeCoupons": 2, "totalUsages": 3, "totalDiscount": 12000 },
          "giftCodes": { "totalCodes": 8, "byStatus": [{ "status": 0, "count": 5 }] }
        }
        """;

        using var document = JsonDocument.Parse(json);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payments = document.RootElement.GetProperty("payments").Deserialize<PaymentsReportModel>(options)!;
        var wallet = document.RootElement.GetProperty("wallet").Deserialize<WalletReportModel>(options)!;
        var coupons = document.RootElement.GetProperty("coupons").Deserialize<CouponsReportModel>(options)!;
        var giftCodes = document.RootElement.GetProperty("giftCodes").Deserialize<GiftCodesReportModel>(options)!;

        payments.PaidAmount.Should().Be(90000m);
        payments.PaidPayments.Should().Be(2);
        wallet.CurrentBalance.Should().Be(70000m);
        wallet.TransactionsCount.Should().Be(2);
        coupons.TotalUsages.Should().Be(3);
        coupons.TotalDiscount.Should().Be(12000m);
        giftCodes.ByStatus.Should().ContainSingle().Which.Count.Should().Be(5);
    }

    [Fact]
    public async Task Dashboard_excludes_unsubmitted_verification_drafts_and_non_paid_orders_from_metrics()
    {
        await using var db = NewContext();
        var today = DateTime.UtcNow.Date.AddHours(10);

        db.Orders.AddRange(
            new Order
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), OrderNumber = "VT-PAID",
                Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid,
                FinalAmount = 450000m, PaidAt = today, CreatedAt = today
            },
            new Order
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), OrderNumber = "VT-REFUNDED",
                Status = (byte)OrderStatus.Refunded, PaymentStatus = (byte)PaymentStatus.Refunded,
                FinalAmount = 800000m, PaidAt = today, CreatedAt = today
            });

        db.UserVerificationProfiles.AddRange(
            new UserVerificationProfile
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), FirstName = "درخواست", LastName = "ارسال‌شده",
                NationalCode = "001", Status = (byte)VerificationStatus.Pending, CreatedAt = today,
                SubmittedAt = today, EncryptedPayload = "encrypted"
            },
            new UserVerificationProfile
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), FirstName = "پیش", LastName = "نویس",
                NationalCode = "002", Status = (byte)VerificationStatus.Pending, CreatedAt = today
            });
        await db.SaveChangesAsync();

        var dashboard = await new AdminDashboardService(db).GetDashboardAsync();

        dashboard.Summary.RevenueToday.Should().Be(450000m);
        dashboard.Summary.PendingVerifications.Should().Be(1);
    }

    private static VitorizeDbContext NewContext() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseInMemoryDatabase($"admin-metrics-{Guid.NewGuid():N}").Options);
}
