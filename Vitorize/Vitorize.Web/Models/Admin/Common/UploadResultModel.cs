namespace Vitorize.Web.Models.Admin.Common
{
    public class UploadResultModel
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long Size { get; set; }
    }
}
