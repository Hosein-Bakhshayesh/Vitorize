using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitorize.Application.DTOs.Admin.GiftCodes;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminGiftCodeService
    {
        Task<GiftCodeBatchDto> ImportAsync(
            GiftCodeImportDto request);

        Task<List<GiftCodeBatchDto>> GetBatchesAsync();

        Task DeleteBatchAsync(Guid batchId);
    }
}
