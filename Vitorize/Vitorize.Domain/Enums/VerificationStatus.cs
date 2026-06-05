using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum VerificationStatus : byte
    {
        Pending = 0,
        Verified = 1,
        Rejected = 2
    }
}
