namespace Vitorize.Web.Services.Storage
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        private const long MaxFileSize = 10 * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".pdf",
            ".zip",
            ".rar"
        };

        public LocalFileStorageService(
            IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string?> SaveAsync(
            IFormFile? file,
            string folder,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > MaxFileSize)
                throw new InvalidOperationException("حجم فایل مجاز نیست.");

            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("فرمت فایل مجاز نیست.");

            var webRoot = GetWebRoot();

            var safeFolder = folder
                .Replace("\\", "/")
                .Trim('/')
                .Replace("..", "");

            var targetDirectory = Path.Combine(
                webRoot,
                "uploads",
                safeFolder);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var fullPath =
                Path.Combine(targetDirectory, fileName);

            await using var stream =
                new FileStream(fullPath, FileMode.Create);

            await file.CopyToAsync(
                stream,
                cancellationToken);

            return $"/uploads/{safeFolder}/{fileName}";
        }

        public Task DeleteAsync(
            string? relativePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return Task.CompletedTask;

            var fullPath = Path.Combine(
                GetWebRoot(),
                relativePath.TrimStart('/'));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return Task.CompletedTask;
        }

        public bool Exists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            var fullPath = Path.Combine(
                GetWebRoot(),
                relativePath.TrimStart('/'));

            return File.Exists(fullPath);
        }

        private string GetWebRoot()
        {
            var webRoot = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");
            }

            if (!Directory.Exists(webRoot))
            {
                Directory.CreateDirectory(webRoot);
            }

            return webRoot;
        }
    }
}