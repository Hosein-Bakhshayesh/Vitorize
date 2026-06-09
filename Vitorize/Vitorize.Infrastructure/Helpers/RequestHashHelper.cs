using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vitorize.Infrastructure.Helpers
{
    public static class RequestHashHelper
    {
        public static string ComputeHash(object? request)
        {
            var json = JsonSerializer.Serialize(request);

            var bytes = Encoding.UTF8.GetBytes(json);

            var hashBytes = SHA256.HashData(bytes);

            return Convert.ToBase64String(hashBytes);
        }
    }
}