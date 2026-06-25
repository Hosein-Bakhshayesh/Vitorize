using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.System;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.SecurityLogs
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<AdminSecurityLogModel> Items { get; set; } = new();
        [BindProperty(SupportsGet=true)] public AdminLogFilterModel Filter { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public async Task OnGetAsync()
        {
            var url = "admin/security-logs" + BuildQuery();
            var result = await _apiClient.GetAsync<List<AdminSecurityLogModel>>(url);
            if (result.IsSuccess && result.Data != null) Items = result.Data; else ErrorMessage = result.Message;
        }
        private string BuildQuery()
        {
            var q = new List<string>();
            if(!string.IsNullOrWhiteSpace(Filter.Search)) q.Add("Search=" + Uri.EscapeDataString(Filter.Search));
            if(Filter.DateFrom.HasValue) q.Add("DateFrom=" + Uri.EscapeDataString(Filter.DateFrom.Value.ToString("yyyy-MM-dd")));
            if(Filter.DateTo.HasValue) q.Add("DateTo=" + Uri.EscapeDataString(Filter.DateTo.Value.ToString("yyyy-MM-dd")));
            if(Filter.IsSuccessful.HasValue) q.Add("IsSuccessful=" + Filter.IsSuccessful.Value.ToString().ToLowerInvariant());
            if(Filter.IsRead.HasValue) q.Add("IsRead=" + Filter.IsRead.Value.ToString().ToLowerInvariant());
            if(Filter.Status.HasValue) q.Add("Status=" + Filter.Status.Value);
            q.Add("PageSize=200");
            return q.Count == 0 ? string.Empty : "?" + string.Join("&", q);
        }
    }
}
