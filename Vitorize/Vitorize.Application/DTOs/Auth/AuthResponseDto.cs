using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Application.DTOs.Auth
{
    public class AuthResponseDto
    {
        public Guid UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Mobile { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTime AccessTokenExpiresAt { get; set; }

        public DateTime RefreshTokenExpiresAt { get; set; }
    }
}
