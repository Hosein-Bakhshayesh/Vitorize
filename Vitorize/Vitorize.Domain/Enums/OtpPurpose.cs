using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum OtpPurpose : byte
    {
        MobileVerification = 1,
        ForgotPassword = 2,
        TwoFactorAuthentication = 3
    }
}
