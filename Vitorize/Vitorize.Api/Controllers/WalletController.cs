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
        private readonly IWalletTopUpService _walletTopUpService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWebHostEnvironment _environment;

        public WalletController(
            IWalletService walletService,
            IWalletTopUpService walletTopUpService,
            ICurrentUserService currentUserService,
            IWebHostEnvironment environment)
        {
            _walletService = walletService;
            _walletTopUpService = walletTopUpService;
            _currentUserService = currentUserService;
            _environment = environment;
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

        [HttpPost("topup")]
        public async Task<ActionResult<ApiResult<WalletTopUpStartResultDto>>> StartTopUp(
            WalletTopUpRequestDto request)
        {
            var userId = GetUserId();

            var result = await _walletTopUpService.StartAsync(userId, request);

            return Ok(ApiResult<WalletTopUpStartResultDto>.Success(
                result,
                "درخواست شارژ کیف پول با موفقیت ایجاد شد."));
        }

        [HttpPost("topup/mock/verify/{topUpId:guid}")]
        public async Task<ActionResult<ApiResult<WalletTopUpVerifyResultDto>>> VerifyMockTopUp(
            Guid topUpId)
        {
            // Dev-only: mock top-up credits the wallet without a real gateway. Never in production.
            if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
                throw new NotFoundException("مسیر مورد نظر یافت نشد.");

            var userId = GetUserId();

            var result = await _walletTopUpService.VerifyMockAsync(userId, topUpId);

            return Ok(ApiResult<WalletTopUpVerifyResultDto>.Success(
                result,
                "شارژ کیف پول با موفقیت انجام شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}
