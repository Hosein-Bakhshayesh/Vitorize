using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Roles;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Roles {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class DetailsModel : PageModel {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient)=>_apiClient=apiClient;
        public AdminRoleModel Role{
            get;
            set;
        }
        =new();
        public async Task<IActionResult> OnGetAsync(Guid id){
            var r=await _apiClient.GetAsync<AdminRoleModel>($"admin/roles/{id}");
            if(!r.IsSuccess||r.Data==null){
                TempData["ErrorMessage"]=r.Message;
                return RedirectToPage("Index");
            }
            Role=r.Data;
            return Page();
        }
    }
}
