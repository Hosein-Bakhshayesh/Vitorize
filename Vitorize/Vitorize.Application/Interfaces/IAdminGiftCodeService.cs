using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitorize.Application.DTOs.Admin.GiftCodes;
using Vitorize.Shared.Common;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminGiftCodeService
    {
        Task<GiftCodeBatchDto> ImportAsync(
            GiftCodeImportDto request);

        Task<List<GiftCodeBatchDto>> GetBatchesAsync();

        Task<GiftCodeBatchDto> GetBatchByIdAsync(Guid batchId);

        Task<PagedResult<AdminGiftCodeDto>> GetGiftCodesAsync(
            AdminGiftCodeFilterDto filter);

        Task DeleteBatchAsync(Guid batchId);
    }
}
