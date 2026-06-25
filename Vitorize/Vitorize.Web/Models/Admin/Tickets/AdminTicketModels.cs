using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Tickets
{
    public class TicketModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string Subject { get; set; } = string.Empty;
        public byte Department { get; set; }
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public List<TicketMessageModel> Messages { get; set; } = new();
        public List<TicketMessageModel> TicketMessages { get; set; } = new();
    }

    public class TicketMessageModel
    {
        public Guid Id { get; set; }
        public Guid TicketId { get; set; }
        public Guid SenderUserId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public bool IsInternalNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminAddTicketMessageRequestModel
    {
        [Required(ErrorMessage = "متن پاسخ الزامی است.")]
        public string Message { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public bool IsInternalNote { get; set; }
    }
}
