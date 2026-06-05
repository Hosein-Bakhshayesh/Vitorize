using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum GiftCodeStatus : byte
    {
        Available = 0,

        Reserved = 1,

        Sold = 2,

        Delivered = 3,

        Expired = 4,

        Disabled = 5
    }
}
