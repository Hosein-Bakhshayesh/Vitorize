namespace Vitorize.Application.Common
{
    public sealed class BootstrapAdminOptions
    {
        public const string SectionName = "BootstrapAdmin";

        public bool Enabled { get; set; }

        public string? Mobile { get; set; }

        public string? Password { get; set; }

        public string? FullName { get; set; }
    }

    public sealed class DevelopmentDemoUserOptions
    {
        public const string SectionName = "DevelopmentDemoUser";

        public bool Enabled { get; set; }

        public string? Mobile { get; set; }

        public string? Password { get; set; }

        public string? FullName { get; set; }
    }
}
