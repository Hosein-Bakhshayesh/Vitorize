using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Vitorize.Application.Interfaces;

namespace Vitorize.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User =>
            _httpContextAccessor.HttpContext?.User;

        public Guid? UserId
        {
            get
            {
                var userId =
                    User?.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                    User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    User?.FindFirstValue("sub");

                return Guid.TryParse(userId, out var id)
                    ? id
                    : null;
            }
        }

        public string? Mobile =>
            User?.FindFirstValue("mobile");

        public string? FullName =>
            User?.FindFirstValue("fullname");

        public bool IsAuthenticated =>
            User?.Identity?.IsAuthenticated ?? false;

        public string? IpAddress
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;

                if (context == null)
                    return null;

                var forwardedFor = context.Request.Headers["X-Forwarded-For"]
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(forwardedFor))
                    return forwardedFor.Split(',').FirstOrDefault()?.Trim();

                return context.Connection.RemoteIpAddress?.ToString();
            }
        }

        public string? UserAgent
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;

                if (context == null)
                    return null;

                return context.Request.Headers.UserAgent.ToString();
            }
        }
    }
}