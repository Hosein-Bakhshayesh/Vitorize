using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Domain.Enums
{
    public enum ProductType : byte
    {
        GiftCard = 1,
        GameAccount = 2,
        GameService = 3,
        Subscription = 4,
        Other = 99
    }
}
