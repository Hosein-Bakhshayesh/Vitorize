using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Tickets;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/tickets")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly ICurrentUserService _currentUserService;

        public TicketsController(
            ITicketService ticketService,
            ICurrentUserService currentUserService)
        {
            _ticketService = ticketService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<TicketDto>>>> GetMyTickets()
        {
            var result = await _ticketService.GetMyTicketsAsync(GetUserId());

            return Ok(ApiResult<List<TicketDto>>.Success(
                result,
                "لیست تیکت‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("{ticketId:guid}")]
        public async Task<ActionResult<ApiResult<TicketDto>>> GetMyTicket(Guid ticketId)
        {
            var result = await _ticketService.GetMyTicketByIdAsync(
                GetUserId(),
                ticketId);

            return Ok(ApiResult<TicketDto>.Success(
                result,
                "جزئیات تیکت با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<TicketDto>>> Create(
            CreateTicketRequestDto request)
        {
            var result = await _ticketService.CreateAsync(
                GetUserId(),
                request);

            return Ok(ApiResult<TicketDto>.Success(
                result,
                "تیکت با موفقیت ثبت شد."));
        }

        [HttpPost("{ticketId:guid}/messages")]
        public async Task<ActionResult<ApiResult<TicketDto>>> AddMessage(
            Guid ticketId,
            AddTicketMessageRequestDto request)
        {
            var result = await _ticketService.AddMessageAsync(
                GetUserId(),
                ticketId,
                request);

            return Ok(ApiResult<TicketDto>.Success(
                result,
                "پیام با موفقیت ثبت شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}