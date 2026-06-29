using Vitorize.Shared.Enums;

namespace Vitorize.Web.Helpers
{
    public static class AdminUiHelper
    {
        public static string Money(
            decimal value,
            byte currencyType = (byte)Vitorize.Shared.Enums.CurrencyType.Toman)
        {
            var unit = currencyType == (byte)Vitorize.Shared.Enums.CurrencyType.Rial
                ? "ریال"
                : "تومان";

            return $"{value:N0} {unit}";
        }

        public static string Date(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy/MM/dd HH:mm")
                : "-";
        }

        public static string Date(DateTime value)
        {
            return value.ToString("yyyy/MM/dd HH:mm");
        }

        public static string ShortText(string? value, int length = 90)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";

            value = value.Trim();

            return value.Length <= length
                ? value
                : value[..length] + "...";
        }

        public static string Image(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim();

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            return "/" + path.TrimStart('~', '/');
        }

        public static string ImageUrl(string? path)
        {
            return Image(path);
        }

        public static string ShortDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy/MM/dd")
                : "-";
        }

        public static string ShortDate(DateTime value)
        {
            return value.ToString("yyyy/MM/dd");
        }

        public static string BadgeClass(string intent)
        {
            return intent switch
            {
                "success" => "vz-badge vz-badge-success",
                "danger" => "vz-badge vz-badge-danger",
                "warning" => "vz-badge vz-badge-warning",
                "info" => "vz-badge vz-badge-info",
                "primary" => "vz-badge vz-badge-primary",
                _ => "vz-badge vz-badge-muted"
            };
        }

        public static string StatusBadgeClass(byte status)
        {
            return status switch
            {
                0 => BadgeClass("muted"),
                1 => BadgeClass("warning"),
                2 => BadgeClass("success"),
                3 => BadgeClass("info"),
                4 => BadgeClass("danger"),
                5 => BadgeClass("danger"),
                6 => BadgeClass("primary"),
                _ => BadgeClass("muted")
            };
        }

        public static string StatusBadgeClass(bool isActive)
        {
            return isActive
                ? BadgeClass("success")
                : BadgeClass("muted");
        }

        public static string ProductType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.ProductType.GiftCard => "گیفت کارت",
                (byte)Vitorize.Shared.Enums.ProductType.GameAccount => "اکانت بازی",
                (byte)Vitorize.Shared.Enums.ProductType.GameService => "سرویس بازی",
                (byte)Vitorize.Shared.Enums.ProductType.Subscription => "اشتراک",
                (byte)Vitorize.Shared.Enums.ProductType.Other => "سایر",
                _ => "نامشخص"
            };
        }

        public static string DeliveryType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.DeliveryType.Instant => "تحویل آنی",
                (byte)Vitorize.Shared.Enums.DeliveryType.Manual => "تحویل دستی",
                (byte)Vitorize.Shared.Enums.DeliveryType.SupportRequired => "نیازمند پشتیبانی",
                _ => "نامشخص"
            };
        }

        public static string CurrencyType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.CurrencyType.Rial => "ریال",
                (byte)Vitorize.Shared.Enums.CurrencyType.Toman => "تومان",
                _ => "نامشخص"
            };
        }

        public static string Currency(byte value)
        {
            return CurrencyType(value);
        }

        public static string VariantStockMode(byte value)
        {
            return value switch
            {
                (byte)ProductVariantStockMode.GiftCode => "وابسته به کد",
                (byte)ProductVariantStockMode.Manual => "موجودی دستی",
                (byte)ProductVariantStockMode.Unlimited => "بدون محدودیت",
                _ => "نامشخص"
            };
        }

        public static string StockMode(byte value)
        {
            return VariantStockMode(value);
        }

        public static string GiftCodeStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.GiftCodeStatus.Available => "آماده فروش",
                (byte)Vitorize.Shared.Enums.GiftCodeStatus.Reserved => "رزرو شده",
                (byte)Vitorize.Shared.Enums.GiftCodeStatus.Sold => "فروخته شده",
                (byte)Vitorize.Shared.Enums.GiftCodeStatus.Delivered => "تحویل شده",
                (byte)Vitorize.Shared.Enums.GiftCodeStatus.Expired => "منقضی",
                (byte)Vitorize.Shared.Enums.GiftCodeStatus.Disabled => "غیرفعال",
                _ => "نامشخص"
            };
        }

        public static string ReservationStatus(byte value)
        {
            return value switch
            {
                (byte)GiftCodeReservationStatus.Released => "آزاد شده",
                (byte)GiftCodeReservationStatus.Active => "فعال",
                (byte)GiftCodeReservationStatus.Sold => "فروخته شده",
                (byte)GiftCodeReservationStatus.Expired => "منقضی",
                _ => "نامشخص"
            };
        }

        public static string OrderStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.OrderStatus.PendingPayment => "در انتظار پرداخت",
                (byte)Vitorize.Shared.Enums.OrderStatus.Processing => "در حال پردازش",
                (byte)Vitorize.Shared.Enums.OrderStatus.Completed => "تکمیل شده",
                (byte)Vitorize.Shared.Enums.OrderStatus.Cancelled => "لغو شده",
                (byte)Vitorize.Shared.Enums.OrderStatus.Failed => "ناموفق",
                (byte)Vitorize.Shared.Enums.OrderStatus.Refunded => "بازگشت وجه",
                _ => "نامشخص"
            };
        }

        public static string PaymentStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.PaymentStatus.Pending => "در انتظار پرداخت",
                (byte)Vitorize.Shared.Enums.PaymentStatus.Paid => "پرداخت شده",
                (byte)Vitorize.Shared.Enums.PaymentStatus.Failed => "ناموفق",
                (byte)Vitorize.Shared.Enums.PaymentStatus.Cancelled => "لغو شده",
                (byte)Vitorize.Shared.Enums.PaymentStatus.Refunded => "بازگشت وجه",
                _ => "نامشخص"
            };
        }

        public static string DeliveryStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.DeliveryStatus.Pending => "در انتظار تحویل",
                (byte)Vitorize.Shared.Enums.DeliveryStatus.Delivered => "تحویل شده",
                (byte)Vitorize.Shared.Enums.DeliveryStatus.ManualReview => "بررسی دستی",
                (byte)Vitorize.Shared.Enums.DeliveryStatus.Failed => "ناموفق",
                _ => "نامشخص"
            };
        }

        public static string DiscountType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.DiscountType.Percentage => "درصدی",
                (byte)Vitorize.Shared.Enums.DiscountType.FixedAmount => "مبلغ ثابت",
                _ => "نامشخص"
            };
        }

        public static string UserStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.UserStatus.Inactive => "غیرفعال",
                (byte)Vitorize.Shared.Enums.UserStatus.Active => "فعال",
                (byte)Vitorize.Shared.Enums.UserStatus.Suspended => "معلق",
                (byte)Vitorize.Shared.Enums.UserStatus.Blocked => "مسدود",
                _ => "نامشخص"
            };
        }

        public static string VerificationDocumentType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.VerificationDocumentType.NationalCardFront => "روی کارت ملی",
                (byte)Vitorize.Shared.Enums.VerificationDocumentType.NationalCardBack => "پشت کارت ملی",
                (byte)Vitorize.Shared.Enums.VerificationDocumentType.SelfieWithNationalCard => "سلفی با کارت ملی",
                (byte)Vitorize.Shared.Enums.VerificationDocumentType.BankCard => "کارت بانکی",
                (byte)Vitorize.Shared.Enums.VerificationDocumentType.Other => "سایر مدارک",
                _ => "نامشخص"
            };
        }

        public static string VerificationStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.VerificationStatus.Pending => "در انتظار بررسی",
                (byte)Vitorize.Shared.Enums.VerificationStatus.Verified => "تأیید شده",
                (byte)Vitorize.Shared.Enums.VerificationStatus.Rejected => "رد شده",
                _ => "نامشخص"
            };
        }

        public static string TicketDepartment(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.TicketDepartment.General => "عمومی",
                (byte)Vitorize.Shared.Enums.TicketDepartment.Orders => "سفارش‌ها",
                (byte)Vitorize.Shared.Enums.TicketDepartment.Payment => "پرداخت",
                (byte)Vitorize.Shared.Enums.TicketDepartment.Verification => "احراز هویت",
                (byte)Vitorize.Shared.Enums.TicketDepartment.Technical => "فنی",
                _ => "نامشخص"
            };
        }

        public static string TicketPriority(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.TicketPriority.Low => "کم",
                (byte)Vitorize.Shared.Enums.TicketPriority.Normal => "معمولی",
                (byte)Vitorize.Shared.Enums.TicketPriority.High => "زیاد",
                (byte)Vitorize.Shared.Enums.TicketPriority.Critical => "بحرانی",
                _ => "نامشخص"
            };
        }

        public static string TicketStatus(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.TicketStatus.Open => "باز",
                (byte)Vitorize.Shared.Enums.TicketStatus.WaitingForAdmin => "در انتظار پاسخ مدیر",
                (byte)Vitorize.Shared.Enums.TicketStatus.WaitingForCustomer => "در انتظار مشتری",
                (byte)Vitorize.Shared.Enums.TicketStatus.Closed => "بسته شده",
                _ => "نامشخص"
            };
        }

        public static string WalletTransactionType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.WalletTransactionType.Credit => "واریز",
                (byte)Vitorize.Shared.Enums.WalletTransactionType.Debit => "برداشت",
                _ => "نامشخص"
            };
        }

        public static string WalletReferenceType(byte? value)
        {
            if (!value.HasValue)
                return "-";

            return value.Value switch
            {
                (byte)Vitorize.Shared.Enums.WalletReferenceType.ManualAdminCharge => "شارژ مدیریتی",
                (byte)Vitorize.Shared.Enums.WalletReferenceType.ManualAdminWithdraw => "برداشت مدیریتی",
                (byte)Vitorize.Shared.Enums.WalletReferenceType.OrderPayment => "پرداخت سفارش",
                (byte)Vitorize.Shared.Enums.WalletReferenceType.Refund => "بازگشت وجه",
                (byte)Vitorize.Shared.Enums.WalletReferenceType.Cashback => "کش‌بک",
                _ => "نامشخص"
            };
        }

        public static string NotificationType(byte value)
        {
            return value switch
            {
                (byte)Vitorize.Shared.Enums.NotificationType.OrderCreated => "ثبت سفارش",
                (byte)Vitorize.Shared.Enums.NotificationType.OrderPaid => "پرداخت سفارش",
                (byte)Vitorize.Shared.Enums.NotificationType.OrderCompleted => "تکمیل سفارش",
                (byte)Vitorize.Shared.Enums.NotificationType.OrderCancelled => "لغو سفارش",

                (byte)Vitorize.Shared.Enums.NotificationType.PaymentSucceeded => "پرداخت موفق",
                (byte)Vitorize.Shared.Enums.NotificationType.PaymentFailed => "پرداخت ناموفق",

                (byte)Vitorize.Shared.Enums.NotificationType.GiftCodeDelivered => "تحویل کد",

                (byte)Vitorize.Shared.Enums.NotificationType.WalletCharged => "شارژ کیف پول",
                (byte)Vitorize.Shared.Enums.NotificationType.WalletDebited => "برداشت کیف پول",
                (byte)Vitorize.Shared.Enums.NotificationType.WalletRefunded => "بازگشت کیف پول",

                (byte)Vitorize.Shared.Enums.NotificationType.VerificationSubmitted => "ثبت احراز هویت",
                (byte)Vitorize.Shared.Enums.NotificationType.VerificationApproved => "تأیید احراز هویت",
                (byte)Vitorize.Shared.Enums.NotificationType.VerificationRejected => "رد احراز هویت",

                (byte)Vitorize.Shared.Enums.NotificationType.TicketCreated => "ثبت تیکت",
                (byte)Vitorize.Shared.Enums.NotificationType.TicketReply => "پاسخ تیکت",
                (byte)Vitorize.Shared.Enums.NotificationType.TicketClosed => "بستن تیکت",

                (byte)Vitorize.Shared.Enums.NotificationType.SystemMessage => "پیام سیستم",
                _ => "پیام"
            };
        }

        public static string StatusBadgeIntent(byte status, string kind)
        {
            return kind switch
            {
                "payment" => status == (byte)Vitorize.Shared.Enums.PaymentStatus.Paid
                    ? "success"
                    : status == (byte)Vitorize.Shared.Enums.PaymentStatus.Pending
                        ? "warning"
                        : "danger",

                "order" => status == (byte)Vitorize.Shared.Enums.OrderStatus.Completed
                    ? "success"
                    : status == (byte)Vitorize.Shared.Enums.OrderStatus.Processing
                        ? "info"
                        : status == (byte)Vitorize.Shared.Enums.OrderStatus.PendingPayment
                            ? "warning"
                            : "danger",

                "ticket" => status == (byte)Vitorize.Shared.Enums.TicketStatus.Closed
                    ? "muted"
                    : status == (byte)Vitorize.Shared.Enums.TicketStatus.WaitingForAdmin
                        ? "warning"
                        : "info",

                "verify" => status == (byte)Vitorize.Shared.Enums.VerificationStatus.Verified
                    ? "success"
                    : status == (byte)Vitorize.Shared.Enums.VerificationStatus.Rejected
                        ? "danger"
                        : "warning",

                "user" => status == (byte)Vitorize.Shared.Enums.UserStatus.Active
                    ? "success"
                    : status == (byte)Vitorize.Shared.Enums.UserStatus.Inactive
                        ? "muted"
                        : "danger",

                _ => "muted"
            };
        }

        public static string ReviewStatusText(bool isApproved, bool isRejected)
        {
            if (isApproved)
                return "تأیید شده";

            if (isRejected)
                return "رد شده";

            return "در انتظار بررسی";
        }

        public static string ReviewStatusIntent(bool isApproved, bool isRejected)
        {
            if (isApproved)
                return "success";

            if (isRejected)
                return "danger";

            return "warning";
        }

        public static string Stars(byte rating)
        {
            if (rating > 5)
                rating = 5;

            return new string('★', rating) + new string('☆', 5 - rating);
        }
    }
}
