using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Email;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services;

/// <summary>Queues both post-payment order emails in the same transaction as the paid order.</summary>
public sealed class OrderEmailOutboxEnqueuer : IOrderEmailOutboxEnqueuer
{
    private const string AdminRecipientKey = "OrderNotificationEmail";
    private const string DefaultAdminRecipient = "vitorize.com@gmail.com";
    private const string CustomerPurpose = "OrderCustomerEmail";
    private const string AdminPurpose = "OrderAdminEmail";

    private readonly VitorizeDbContext _dbContext;

    public OrderEmailOutboxEnqueuer(VitorizeDbContext dbContext) => _dbContext = dbContext;

    public async Task EnqueuePaidOrderEmailsAsync(PaidOrderEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.OrderNumber)) return;

        if (IsValidEmail(request.CustomerEmail))
        {
            await EnqueueOnceAsync(request.OrderId, CustomerPurpose, new EmailOutboxPayload
            {
                Recipient = request.CustomerEmail!.Trim(),
                Subject = $"تأیید سفارش {request.OrderNumber} | ویتورایز",
                Body = BuildCustomerBody(request)
            }, cancellationToken);
        }

        var adminRecipient = await _dbContext.Settings.AsNoTracking()
            .Where(x => x.Key == AdminRecipientKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
        adminRecipient = IsValidEmail(adminRecipient) ? adminRecipient!.Trim() : DefaultAdminRecipient;

        await EnqueueOnceAsync(request.OrderId, AdminPurpose, new EmailOutboxPayload
        {
            Recipient = adminRecipient,
            Subject = $"سفارش جدید {request.OrderNumber} | ویتورایز",
            Body = BuildAdminBody(request)
        }, cancellationToken);
    }

    private async Task EnqueueOnceAsync(Guid orderId, string purpose, EmailOutboxPayload payload, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.OutboxMessages.AnyAsync(x =>
            x.MessageType == OutboxMessageTypes.EmailSend &&
            x.AggregateId == orderId &&
            x.AggregateType == purpose, cancellationToken);
        if (exists) return;

        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageTypes.EmailSend,
            AggregateId = orderId,
            AggregateType = purpose,
            Payload = JsonSerializer.Serialize(payload),
            Status = (byte)OutboxMessageStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    private static string BuildCustomerBody(PaidOrderEmailRequest request) =>
        $"{request.CustomerName} عزیز،\n\n" +
        $"پرداخت سفارش شما با موفقیت انجام شد.\n" +
        $"شماره سفارش: {request.OrderNumber}\n" +
        $"مبلغ نهایی: {Money(request.FinalAmount)} تومان\n\n" +
        $"اقلام سفارش:\n{Items(request.Items)}\n\n" +
        "برای پیگیری سفارش، وارد حساب کاربری خود در ویتورایز شوید.\n\nبا تشکر، ویتورایز";

    private static string BuildAdminBody(PaidOrderEmailRequest request) =>
        "یک سفارش جدید با پرداخت موفق ثبت شد.\n\n" +
        $"شماره سفارش: {request.OrderNumber}\n" +
        $"مشتری: {request.CustomerName}\n" +
        $"موبایل: {request.CustomerMobile}\n" +
        $"ایمیل: {request.CustomerEmail ?? "—"}\n" +
        $"مبلغ نهایی: {Money(request.FinalAmount)} تومان\n\n" +
        $"اقلام سفارش:\n{Items(request.Items)}";

    private static string Items(IEnumerable<PaidOrderEmailItem> items) => string.Join("\n", items.Select(item =>
        $"• {item.ProductTitle}{(string.IsNullOrWhiteSpace(item.VariantTitle) ? string.Empty : $" — {item.VariantTitle}")} | تعداد: {item.Quantity.ToString(CultureInfo.InvariantCulture)} | مبلغ واحد: {Money(item.UnitPrice)} تومان"));

    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { _ = new MailAddress(value.Trim()); return true; }
        catch (FormatException) { return false; }
    }
}
