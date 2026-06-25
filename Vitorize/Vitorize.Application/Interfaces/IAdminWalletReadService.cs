using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.DTOs.Admin.Wallets;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminWalletReadService
    {
        Task<List<AdminWalletListDto>> GetAllAsync(AdminQueryFilterDto filter);
    }
}
