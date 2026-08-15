namespace Vitorize.Api.BackgroundServices;

public sealed class KycDeadlineProcessingOptions
{
    public const string SectionName = "KycDeadlineProcessing";
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
}
