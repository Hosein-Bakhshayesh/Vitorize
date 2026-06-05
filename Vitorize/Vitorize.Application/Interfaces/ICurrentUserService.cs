using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }

        string? Mobile { get; }

        string? FullName { get; }

        bool IsAuthenticated { get; }
    }
}