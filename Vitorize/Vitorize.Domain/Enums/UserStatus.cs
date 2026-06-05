using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum UserStatus : byte
    {
        Inactive = 0,
        Active = 1,
        Suspended = 2,
        Blocked = 3
    }
}
