using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Tickets;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/tickets")]
    public class AdminTicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly ICurrentUserService _currentUserService;

        public AdminTicketsController(
            ITicketService ticketService,
            ICurrentUserService currentUserService)
        {
            _ticketService = ticketService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<TicketDto>>>> GetAll()
        {
            var result = await _ticketService.GetAllAsync();
            return Ok(ApiResult<List<TicketDto>>.Success(result, "لیست تیکت‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("{ticketId:guid}")]
        public async Task<ActionResult<ApiResult<TicketDto>>> GetById(Guid ticketId)
        {
            var result = await _ticketService.GetByIdAsync(ticketId);
            return Ok(ApiResult<TicketDto>.Success(result, "جزئیات تیکت با موفقیت دریافت شد."));
        }

        [HttpPost("{ticketId:guid}/messages")]
        public async Task<ActionResult<ApiResult<TicketDto>>> AddMessage(Guid ticketId, AdminAddTicketMessageRequestDto request)
        {
            var result = await _ticketService.AdminAddMessageAsync(GetUserId(), ticketId, request);
            return Ok(ApiResult<TicketDto>.Success(result, "پاسخ تیکت با موفقیت ثبت شد."));
        }

        [HttpPost("{ticketId:guid}/close")]
        public async Task<ActionResult<ApiResult<TicketDto>>> Close(Guid ticketId)
        {
            var result = await _ticketService.CloseAsync(ticketId);
            return Ok(ApiResult<TicketDto>.Success(result, "تیکت با موفقیت بسته شد."));
        }

        [HttpPost("{ticketId:guid}/reopen")]
        public async Task<ActionResult<ApiResult<TicketDto>>> Reopen(Guid ticketId)
        {
            var result = await _ticketService.ReopenAsync(ticketId);
            return Ok(ApiResult<TicketDto>.Success(result, "تیکت با موفقیت باز شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");
            return _currentUserService.UserId.Value;
        }
    }
}
