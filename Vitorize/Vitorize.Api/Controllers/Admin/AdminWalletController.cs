using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.DTOs.Admin.Wallets;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/wallets")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminWalletController : ControllerBase
    {
        private readonly IWalletService _walletService;
        private readonly IAdminWalletReadService _walletReadService;

        public AdminWalletController(
            IWalletService walletService,
            IAdminWalletReadService walletReadService)
        {
            _walletService = walletService;
            _walletReadService = walletReadService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminWalletListDto>>>> GetAll(
            [FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _walletReadService.GetAllAsync(filter);

            return Ok(ApiResult<List<AdminWalletListDto>>.Success(
                result,
                "کیف پول‌ها با موفقیت دریافت شدند."));
        }

        [HttpGet("{userId:guid}")]
        public async Task<ActionResult<ApiResult<WalletDto>>> GetUserWallet(Guid userId)
        {
            var result = await _walletService.GetUserWalletAsync(userId);

            return Ok(ApiResult<WalletDto>.Success(
                result,
                "کیف پول کاربر با موفقیت دریافت شد."));
        }

        [HttpGet("{userId:guid}/transactions")]
        public async Task<ActionResult<ApiResult<List<WalletTransactionDto>>>> GetUserTransactions(Guid userId)
        {
            var result = await _walletService.GetUserTransactionsAsync(userId);

            return Ok(ApiResult<List<WalletTransactionDto>>.Success(
                result,
                "تراکنش‌های کیف پول کاربر با موفقیت دریافت شدند."));
        }

        [HttpPost("charge")]
        public async Task<ActionResult<ApiResult<WalletDto>>> Charge(WalletChargeRequestDto request)
        {
            var result = await _walletService.AdminChargeAsync(request);

            return Ok(ApiResult<WalletDto>.Success(
                result,
                "کیف پول کاربر با موفقیت شارژ شد."));
        }

        [HttpPost("withdraw")]
        public async Task<ActionResult<ApiResult<WalletDto>>> Withdraw(WalletWithdrawRequestDto request)
        {
            var result = await _walletService.AdminWithdrawAsync(request);

            return Ok(ApiResult<WalletDto>.Success(
                result,
                "برداشت از کیف پول کاربر با موفقیت انجام شد."));
        }
    }
}
