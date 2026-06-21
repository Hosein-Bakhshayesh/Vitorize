namespace Vitorize.Application.DTOs.Admin.Reports
{
    public class UsersReportDto
    {
        public int TotalUsers { get; set; }

        public int NewUsers { get; set; }

        public int ActiveUsers { get; set; }

        public int BlockedUsers { get; set; }

        public int VerifiedUsers { get; set; }

        public List<UserRegistrationDailyDto> DailyRegistrations { get; set; } = new();
    }

    public class UserRegistrationDailyDto
    {
        public DateTime Date { get; set; }

        public int Count { get; set; }
    }
}