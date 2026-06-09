using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/wallet")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;
        private readonly ICurrentUserService _currentUserService;

        public WalletController(
            IWalletService walletService,
            ICurrentUserService currentUserService)
        {
            _walletService = walletService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<WalletDto>>> GetMyWallet()
        {
            var userId = GetUserId();

            var result = await _walletService.GetMyWalletAsync(userId);

            return Ok(ApiResult<WalletDto>.Success(
                result,
                "کیف پول با موفقیت دریافت شد."));
        }

        [HttpGet("transactions")]
        public async Task<ActionResult<ApiResult<List<WalletTransactionDto>>>> GetMyTransactions()
        {
            var userId = GetUserId();

            var result = await _walletService.GetMyTransactionsAsync(userId);

            return Ok(ApiResult<List<WalletTransactionDto>>.Success(
                result,
                "تراکنش‌های کیف پول با موفقیت دریافت شدند."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}