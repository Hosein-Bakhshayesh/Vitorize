namespace Vitorize.Web.Models.Admin.System
{
    public class AdminLogFilterModel
    {
        public string? Search { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? IsSuccessful { get; set; }
        public bool? IsRead { get; set; }
        public byte? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
    public class AdminErrorLogModel { public Guid Id { get; set; } public string Message { get; set; } = string.Empty; public string? StackTrace { get; set; } public string? Source { get; set; } public DateTime CreatedAt { get; set; } }
    public class AdminAuditLogModel { public Guid Id { get; set; } public Guid? UserId { get; set; } public string? UserFullName { get; set; } public string ActionType { get; set; } = string.Empty; public string EntityName { get; set; } = string.Empty; public string? EntityId { get; set; } public string? Data { get; set; } public string? IpAddress { get; set; } public string? UserAgent { get; set; } public DateTime CreatedAt { get; set; } }
    public class AdminSecurityLogModel { public Guid Id { get; set; } public Guid? UserId { get; set; } public string? UserFullName { get; set; } public string EventType { get; set; } = string.Empty; public string? Description { get; set; } public string? IpAddress { get; set; } public string? UserAgent { get; set; } public bool IsSuccessful { get; set; } public DateTime CreatedAt { get; set; } }
}
