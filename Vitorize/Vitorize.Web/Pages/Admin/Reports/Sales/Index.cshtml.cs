using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Reports;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Reports.Sales {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public SalesReportModel Report{
            get;
            set;
        }
        =new();
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var r=await _apiClient.GetAsync<SalesReportModel>("admin/reports/sales");
            if(r.IsSuccess&&r.Data!=null) Report=r.Data;
            else ErrorMessage=r.Message;
        }
    }
}
