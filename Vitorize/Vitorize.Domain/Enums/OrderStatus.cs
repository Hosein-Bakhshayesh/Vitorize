using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum OrderStatus : byte
    {
        PendingPayment = 1,
        Processing = 2,
        Completed = 3,
        Cancelled = 4,
        Failed = 5,
        Refunded = 6
    }
}
