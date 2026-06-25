using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Verification;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Verifications {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public List<VerificationProfileModel> Items{
            get;
            set;
        }
        =new();
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var r=await _apiClient.GetAsync<List<VerificationProfileModel>>("admin/verifications");
            if(r.IsSuccess&&r.Data!=null) Items=r.Data.OrderByDescending(x=>x.SubmittedAt).ToList();
            else ErrorMessage=r.Message;
        }
    }
}
