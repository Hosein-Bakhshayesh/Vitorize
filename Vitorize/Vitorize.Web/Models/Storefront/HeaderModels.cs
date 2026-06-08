namespace Vitorize.Web.Models.Storefront
{
    public class StoreHeaderModel
    {
        public bool IsAuthenticated { get; set; }

        public string? FullName { get; set; }

        public string? Mobile { get; set; }

        public int CartCount { get; set; }
    }

    public class StoreCurrentUserModel
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Mobile { get; set; } = string.Empty;

        public string? Email { get; set; }

        public byte Status { get; set; }

        public byte VerificationStatus { get; set; }

        public bool IsMobileConfirmed { get; set; }

        public bool IsEmailConfirmed { get; set; }
    }
}