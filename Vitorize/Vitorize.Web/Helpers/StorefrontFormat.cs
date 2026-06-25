using System.Globalization;
using Vitorize.Shared.Enums;

namespace Vitorize.Web.Helpers
{
    public static class StorefrontFormat
    {
        private static readonly CultureInfo Fa = CultureInfo.GetCultureInfo("fa-IR");

        public static string Money(decimal value)
        {
            return $"{value.ToString("N0", Fa)} تومان";
        }

        public static string Number(int value)
        {
            return value.ToString("N0", Fa);
        }

        public static string Date(DateTime value)
        {
            return value.ToString("yyyy/MM/dd", Fa);
        }

        public static string Date(DateTime? value)
        {
            return value.HasValue ? Date(value.Value) : "-";
        }

        public static string ProductImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/img/product-placeholder.svg";

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) || path.StartsWith('/'))
                return path;

            return "/" + path.TrimStart('~', '/');
        }

        public static string OrderStatus(byte status)
        {
            return Enum.IsDefined(typeof(OrderStatus), status)
                ? OrderStatus((OrderStatus)status)
                : "نامشخص";
        }

        public static string OrderStatus(OrderStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.OrderStatus.PendingPayment => "در انتظار پرداخت",
                Vitorize.Shared.Enums.OrderStatus.Processing => "در حال پردازش",
                Vitorize.Shared.Enums.OrderStatus.Completed => "تکمیل شده",
                Vitorize.Shared.Enums.OrderStatus.Cancelled => "لغو شده",
                Vitorize.Shared.Enums.OrderStatus.Failed => "ناموفق",
                Vitorize.Shared.Enums.OrderStatus.Refunded => "بازگشت وجه",
                _ => "نامشخص"
            };
        }

        public static string PaymentStatus(byte status)
        {
            return Enum.IsDefined(typeof(PaymentStatus), status)
                ? PaymentStatus((PaymentStatus)status)
                : "نامشخص";
        }

        public static string PaymentStatus(PaymentStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.PaymentStatus.Pending => "در انتظار پرداخت",
                Vitorize.Shared.Enums.PaymentStatus.Paid => "پرداخت موفق",
                Vitorize.Shared.Enums.PaymentStatus.Failed => "پرداخت ناموفق",
                Vitorize.Shared.Enums.PaymentStatus.Cancelled => "لغو شده",
                Vitorize.Shared.Enums.PaymentStatus.Refunded => "بازگشت وجه",
                _ => "نامشخص"
            };
        }

        public static string DeliveryStatus(byte status)
        {
            return Enum.IsDefined(typeof(DeliveryStatus), status)
                ? DeliveryStatus((DeliveryStatus)status)
                : "نامشخص";
        }

        public static string DeliveryStatus(DeliveryStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.DeliveryStatus.Pending => "در انتظار تحویل",
                Vitorize.Shared.Enums.DeliveryStatus.Delivered => "تحویل شده",
                Vitorize.Shared.Enums.DeliveryStatus.ManualReview => "نیازمند بررسی دستی",
                Vitorize.Shared.Enums.DeliveryStatus.Failed => "تحویل ناموفق",
                _ => "نامشخص"
            };
        }

        public static string DeliveryType(byte type)
        {
            return Enum.IsDefined(typeof(DeliveryType), type)
                ? DeliveryType((DeliveryType)type)
                : "نامشخص";
        }

        public static string DeliveryType(DeliveryType type)
        {
            return type switch
            {
                Vitorize.Shared.Enums.DeliveryType.Instant => "تحویل آنی",
                Vitorize.Shared.Enums.DeliveryType.Manual => "تحویل دستی",
                Vitorize.Shared.Enums.DeliveryType.SupportRequired => "نیازمند پشتیبانی",
                _ => "نامشخص"
            };
        }

        public static string ProductType(byte type)
        {
            return Enum.IsDefined(typeof(ProductType), type)
                ? ProductType((ProductType)type)
                : "نامشخص";
        }

        public static string ProductType(ProductType type)
        {
            return type switch
            {
                Vitorize.Shared.Enums.ProductType.GiftCard => "گیفت کارت",
                Vitorize.Shared.Enums.ProductType.GameAccount => "اکانت بازی",
                Vitorize.Shared.Enums.ProductType.GameService => "سرویس بازی",
                Vitorize.Shared.Enums.ProductType.Subscription => "اشتراک",
                Vitorize.Shared.Enums.ProductType.Other => "سایر",
                _ => "نامشخص"
            };
        }

        public static string Currency(byte currency)
        {
            return Enum.IsDefined(typeof(CurrencyType), currency)
                ? Currency((CurrencyType)currency)
                : "نامشخص";
        }

        public static string Currency(CurrencyType currency)
        {
            return currency switch
            {
                CurrencyType.Rial => "ریال",
                CurrencyType.Toman => "تومان",
                _ => "نامشخص"
            };
        }

        public static string UserStatus(byte status)
        {
            return Enum.IsDefined(typeof(UserStatus), status)
                ? UserStatus((UserStatus)status)
                : "نامشخص";
        }

        public static string UserStatus(UserStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.UserStatus.Inactive => "غیرفعال",
                Vitorize.Shared.Enums.UserStatus.Active => "فعال",
                Vitorize.Shared.Enums.UserStatus.Suspended => "تعلیق شده",
                Vitorize.Shared.Enums.UserStatus.Blocked => "مسدود شده",
                _ => "نامشخص"
            };
        }

        public static string VerificationStatus(byte status)
        {
            return Enum.IsDefined(typeof(VerificationStatus), status)
                ? VerificationStatus((VerificationStatus)status)
                : "نامشخص";
        }

        public static string VerificationStatus(VerificationStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.VerificationStatus.Pending => "در انتظار بررسی",
                Vitorize.Shared.Enums.VerificationStatus.Verified => "تایید شده",
                Vitorize.Shared.Enums.VerificationStatus.Rejected => "رد شده",
                _ => "نامشخص"
            };
        }

        public static string GiftCodeStatus(byte status)
        {
            return Enum.IsDefined(typeof(GiftCodeStatus), status)
                ? GiftCodeStatus((GiftCodeStatus)status)
                : "نامشخص";
        }

        public static string GiftCodeStatus(GiftCodeStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.GiftCodeStatus.Available => "آماده فروش",
                Vitorize.Shared.Enums.GiftCodeStatus.Reserved => "رزرو شده",
                Vitorize.Shared.Enums.GiftCodeStatus.Sold => "فروخته شده",
                Vitorize.Shared.Enums.GiftCodeStatus.Delivered => "تحویل شده",
                Vitorize.Shared.Enums.GiftCodeStatus.Expired => "منقضی شده",
                Vitorize.Shared.Enums.GiftCodeStatus.Disabled => "غیرفعال",
                _ => "نامشخص"
            };
        }

        public static string GiftCodeReservationStatus(byte status)
        {
            return Enum.IsDefined(typeof(GiftCodeReservationStatus), status)
                ? GiftCodeReservationStatus((GiftCodeReservationStatus)status)
                : "نامشخص";
        }

        public static string GiftCodeReservationStatus(GiftCodeReservationStatus status)
        {
            return status switch
            {
                Vitorize.Shared.Enums.GiftCodeReservationStatus.Released => "آزاد شده",
                Vitorize.Shared.Enums.GiftCodeReservationStatus.Active => "فعال",
                Vitorize.Shared.Enums.GiftCodeReservationStatus.Sold => "فروخته شده",
                Vitorize.Shared.Enums.GiftCodeReservationStatus.Expired => "منقضی شده",
                _ => "نامشخص"
            };
        }

        public static string DiscountType(byte type)
        {
            return Enum.IsDefined(typeof(DiscountType), type)
                ? DiscountType((DiscountType)type)
                : "نامشخص";
        }

        public static string DiscountType(DiscountType type)
        {
            return type switch
            {
                Vitorize.Shared.Enums.DiscountType.Percentage => "درصدی",
                Vitorize.Shared.Enums.DiscountType.FixedAmount => "مبلغ ثابت",
                _ => "نامشخص"
            };
        }

        public static string ProductVariantStockMode(byte mode)
        {
            return Enum.IsDefined(typeof(ProductVariantStockMode), mode)
                ? ProductVariantStockMode((ProductVariantStockMode)mode)
                : "نامشخص";
        }

        public static string ProductVariantStockMode(ProductVariantStockMode mode)
        {
            return mode switch
            {
                Vitorize.Shared.Enums.ProductVariantStockMode.GiftCode => "موجودی کد",
                Vitorize.Shared.Enums.ProductVariantStockMode.Manual => "موجودی دستی",
                Vitorize.Shared.Enums.ProductVariantStockMode.Unlimited => "نامحدود",
                _ => "نامشخص"
            };
        }

        public static string OtpPurpose(byte purpose)
        {
            return Enum.IsDefined(typeof(OtpPurpose), purpose)
                ? OtpPurpose((OtpPurpose)purpose)
                : "نامشخص";
        }

        public static string OtpPurpose(OtpPurpose purpose)
        {
            return purpose switch
            {
                Vitorize.Shared.Enums.OtpPurpose.MobileVerification => "تایید موبایل",
                Vitorize.Shared.Enums.OtpPurpose.ForgotPassword => "فراموشی رمز عبور",
                Vitorize.Shared.Enums.OtpPurpose.TwoFactorAuthentication => "ورود دو مرحله‌ای",
                _ => "نامشخص"
            };
        }
    }
}
