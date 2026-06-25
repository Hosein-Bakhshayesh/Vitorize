using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Notifications;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Notifications {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class DetailsModel : PageModel {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient)=>_apiClient=apiClient;
        public AdminNotificationModel Item{
            get;
            set;
        }
        =new();
        public async Task<IActionResult> OnGetAsync(Guid id){
            var r=await _apiClient.GetAsync<AdminNotificationModel>($"admin/notifications/{id}");
            if(!r.IsSuccess||r.Data==null){
                TempData["ErrorMessage"]=r.Message;
                return RedirectToPage("Index");
            }
            Item=r.Data;
            return Page();
        }
    }
}
