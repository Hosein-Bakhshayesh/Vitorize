namespace Vitorize.Application.DTOs.Admin.Uploads
{
    public class UploadFileResultDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}