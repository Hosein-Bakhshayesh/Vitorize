using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum DeliveryType : byte
    {
        Instant = 1,        // تحویل آنی کد
        ManualTicket = 2    // نیازمند تیکت / ارسال دستی
    }
}
