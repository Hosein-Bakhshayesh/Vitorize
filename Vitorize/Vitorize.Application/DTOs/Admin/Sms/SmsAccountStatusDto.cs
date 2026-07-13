namespace Vitorize.Application.DTOs.Admin.Sms
{
    public class SmsAccountStatusDto
    {
        public bool IsConfigured { get; set; }
        public bool IsEnabled { get; set; }
        public bool ConnectionOk { get; set; }
        public decimal? Credit { get; set; }
        public List<long> Lines { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
