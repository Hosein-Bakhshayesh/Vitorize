namespace Vitorize.Application.DTOs.Settings
{
    public class SettingGroupDto
    {
        public string GroupName { get; set; } = null!;

        public List<SettingDto> Settings { get; set; } = new();
    }
}