using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Application.DTOs.Auth
{
    public class LoginRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
