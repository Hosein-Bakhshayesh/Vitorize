namespace Vitorize.Application.DTOs.Settings
{
    public class SettingDto
    {
        public Guid Id { get; set; }

        public string Key { get; set; } = null!;

        public string? Value { get; set; }

        public string? GroupName { get; set; }

        public string? ValueType { get; set; }

        public string? Description { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}