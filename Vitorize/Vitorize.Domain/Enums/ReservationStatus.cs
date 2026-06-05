using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum ReservationStatus : byte
    {
        Active = 1,

        Released = 2,

        Sold = 3,

        Expired = 4
    }
}
