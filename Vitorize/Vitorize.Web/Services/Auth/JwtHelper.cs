using System.Text;
using System.Text.Json;

namespace Vitorize.Web.Services.Auth
{
    /// <summary>
    /// استخراج نقش‌ها از توکن JWT بدون اعتبارسنجی امضا.
    /// اعتبارسنجی واقعی توکن در سمت API انجام می‌شود.
    /// </summary>
    public static class JwtHelper
    {
        public static IReadOnlyList<string> ExtractRoles(string? token)
        {
            var roles = new List<string>();

            if (string.IsNullOrWhiteSpace(token))
                return roles;

            try
            {
                var parts = token.Split('.');

                if (parts.Length < 2)
                    return roles;

                var payloadJson = DecodeBase64Url(parts[1]);

                using var document = JsonDocument.Parse(payloadJson);

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var name = property.Name;

                    var isRoleClaim =
                        name.Equals("role", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("roles", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("/role", StringComparison.OrdinalIgnoreCase);

                    if (!isRoleClaim)
                        continue;

                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                roles.Add(item.GetString() ?? string.Empty);
                        }
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        roles.Add(property.Value.GetString() ?? string.Empty);
                    }
                }

                return roles
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return roles;
            }
        }

        public static bool IsAdmin(IEnumerable<string> roles)
        {
            return roles.Any(r =>
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase));
        }

        private static string DecodeBase64Url(string value)
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
