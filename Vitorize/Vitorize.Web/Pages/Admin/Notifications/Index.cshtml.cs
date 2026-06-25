using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Notifications;
using Vitorize.Web.Models.Admin.System;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Notifications {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public List<AdminNotificationModel> Items{
            get;
            set;
        }
        =new();
        [BindProperty(SupportsGet=true)] public AdminLogFilterModel Filter{
            get;
            set;
        }
        =new();
        [TempData] public string? SuccessMessage{
            get;
            set;
        }
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var q=new List<string>();
            if(!string.IsNullOrWhiteSpace(Filter.Search))q.Add("Search="+Uri.EscapeDataString(Filter.Search));
            if(Filter.IsRead.HasValue)q.Add("IsRead="+Filter.IsRead.Value.ToString().ToLowerInvariant());
            q.Add("PageSize=200");
            var r=await _apiClient.GetAsync<List<AdminNotificationModel>>("admin/notifications"+(q.Any()?"?"+string.Join("&",q):""));
            if(r.IsSuccess&&r.Data!=null)Items=r.Data;
            else ErrorMessage=r.Message;
        }
        public async Task<IActionResult> OnPostReadAsync(Guid id){
            var r=await _apiClient.PostAsync<object>($"admin/notifications/{id}/read", new{
            }
            );
            TempData[r.IsSuccess?"SuccessMessage":"ErrorMessage"] = r.IsSuccess?"اطلاعیه خوانده شد.":r.Message;
            return RedirectToPage();
        }
    }
}
