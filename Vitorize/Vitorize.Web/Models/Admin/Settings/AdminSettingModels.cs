using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Settings
{
    public class SettingGroupModel
    {
        public string GroupName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public List<SettingModel> Settings { get; set; } = new();
        public List<SettingModel> Items { get; set; } = new();
    }
    public class SettingModel
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? GroupName { get; set; }
        public string? ValueType { get; set; }
        public string? Description { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    public class UpdateSettingModel
    {
        [Required] public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? GroupName { get; set; }
        public string? ValueType { get; set; }
        public string? Description { get; set; }
    }
    public class FontAssetModel
    {
        public Guid Id { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string FileFormat { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsActive { get; set; }
        public byte Scope { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
