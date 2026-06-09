using System.Net;
using System.Text.Json;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IErrorLogService errorLogService)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(
                    context,
                    ex,
                    errorLogService);
            }
        }

        private async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception,
            IErrorLogService errorLogService)
        {
            var statusCode = exception switch
            {
                BusinessException => HttpStatusCode.BadRequest,
                NotFoundException => HttpStatusCode.NotFound,
                UnauthorizedException => HttpStatusCode.Unauthorized,
                _ => HttpStatusCode.InternalServerError
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(
                    exception,
                    exception.Message);

                await errorLogService.LogAsync(exception);
            }

            var response = ApiResult.Failure(
                statusCode == HttpStatusCode.InternalServerError
                    ? "خطای داخلی سرور رخ داده است."
                    : exception.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var json = JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                });

            await context.Response.WriteAsync(json);
        }
    }
}