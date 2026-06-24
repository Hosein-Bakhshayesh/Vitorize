using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vitorize.Web.Constants;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Filters
{
    public class AdminApiAuthorizationFilter : IAsyncPageFilter
    {
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        public async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context,
            PageHandlerExecutionDelegate next)
        {
            var executedContext = await next();

            var httpContext = executedContext.HttpContext;

            if (ShouldSkip(httpContext))
                return;

            if (httpContext.Items.ContainsKey(AdminApiAuthItems.Unauthorized))
            {
                await SignOutAdminAsync(httpContext);

                var returnUrl = BuildReturnUrl(httpContext);

                executedContext.Result = new RedirectToPageResult(
                    "/Admin/Auth/Login",
                    new
                    {
                        returnUrl
                    });

                return;
            }

            if (httpContext.Items.ContainsKey(AdminApiAuthItems.Forbidden))
            {
                executedContext.Result = new RedirectToPageResult(
                    "/Admin/Auth/AccessDenied");

                return;
            }
        }

        private static bool ShouldSkip(HttpContext httpContext)
        {
            var path = httpContext.Request.Path.Value ?? string.Empty;

            if (!path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (path.StartsWith("/Admin/Auth", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string BuildReturnUrl(HttpContext httpContext)
        {
            var pathBase = httpContext.Request.PathBase.Value ?? string.Empty;
            var path = httpContext.Request.Path.Value ?? "/Admin";
            var query = httpContext.Request.QueryString.Value ?? string.Empty;

            return pathBase + path + query;
        }

        private static async Task SignOutAdminAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(VitorizeAuthSchemes.AdminScheme);

            httpContext.Response.Cookies.Delete("Vitorize.Admin.Auth");
            httpContext.Response.Cookies.Delete("Vitorize.Admin.AccessToken");
            httpContext.Response.Cookies.Delete("Vitorize.Admin.RefreshToken");
        }
    }
}