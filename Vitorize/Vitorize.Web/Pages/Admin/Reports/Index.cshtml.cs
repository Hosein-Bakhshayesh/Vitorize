using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Reports;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Reports {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public GiftCodesReportModel GiftCodes{
            get;
            set;
        }
        =new();
        public UsersReportModel Users{
            get;
            set;
        }
        =new();
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var g=await _apiClient.GetAsync<GiftCodesReportModel>("admin/reports/giftcodes");
            if(g.IsSuccess&&g.Data!=null) GiftCodes=g.Data;
            var u=await _apiClient.GetAsync<UsersReportModel>("admin/reports/users");
            if(u.IsSuccess&&u.Data!=null) Users=u.Data;
        }
    }
}
