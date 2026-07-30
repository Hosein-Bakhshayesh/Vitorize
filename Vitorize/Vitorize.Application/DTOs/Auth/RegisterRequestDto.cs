using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Application.DTOs.Auth
{
    public class RegisterRequestDto
    {
        public string FullName { get; set; } = string.Empty;

        public string Mobile { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string Password { get; set; } = string.Empty;
    }
}
