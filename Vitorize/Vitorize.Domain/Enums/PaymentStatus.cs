using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum PaymentStatus : byte
    {
        Pending = 1,
        Paid = 2,
        Failed = 3,
        Cancelled = 4,
        Refunded = 5
    }
}
