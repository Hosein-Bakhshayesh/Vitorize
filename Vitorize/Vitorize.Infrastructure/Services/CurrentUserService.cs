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

        public Guid? UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                var userId =
                    user?.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                    user?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    user?.FindFirstValue("sub");

                return Guid.TryParse(userId, out var id)
                    ? id
                    : null;
            }
        }

        public string? Mobile =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue("mobile");

        public string? FullName =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue("fullname");

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }
}