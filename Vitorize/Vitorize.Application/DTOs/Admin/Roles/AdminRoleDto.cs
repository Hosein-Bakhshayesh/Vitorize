namespace Vitorize.Application.DTOs.Admin.Roles
{
    public class AdminRoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int UsersCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
