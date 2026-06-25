namespace Vitorize.Web.Models.Admin.Roles
{
    public class AdminRoleModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int UsersCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
