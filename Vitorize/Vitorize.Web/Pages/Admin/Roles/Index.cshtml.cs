using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Roles;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Roles {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public List<AdminRoleModel> Roles{
            get;
            set;
        }
        =new();
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var r=await _apiClient.GetAsync<List<AdminRoleModel>>("admin/roles");
            if(r.IsSuccess&&r.Data!=null)Roles=r.Data;
            else ErrorMessage=r.Message;
        }
    }
}
