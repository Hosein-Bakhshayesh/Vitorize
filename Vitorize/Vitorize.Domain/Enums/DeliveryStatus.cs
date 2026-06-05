using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum DeliveryStatus : byte
    {
        Pending = 1,
        Delivered = 2,
        ManualReview = 3,
        Failed = 4
    }
}
