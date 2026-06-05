using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum GiftCodeReservationStatus : byte
    {
        Released = 0,
        Active = 1,
        Sold = 2,
        Expired = 3
    }
}
