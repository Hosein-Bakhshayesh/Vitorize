using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Application.DTOs.Auth
{
    public class CurrentUserDto
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Mobile { get; set; } = string.Empty;

        public string? Email { get; set; }

        public byte Status { get; set; }

        public byte VerificationStatus { get; set; }

        public bool IsMobileConfirmed { get; set; }

        public bool IsEmailConfirmed { get; set; }
    }
}
