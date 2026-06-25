using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Payments;
using Vitorize.Web.Models.Admin.System;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Payments {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public List<AdminPaymentModel> Items{
            get;
            set;
        }
        =new();
        [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet=true)] public AdminLogFilterModel Filter{
            get;
            set;
        }
        =new();
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var q=new List<string>();
            if(!string.IsNullOrWhiteSpace(Filter.Search))q.Add("Search="+Uri.EscapeDataString(Filter.Search));
            if(Filter.Status.HasValue)q.Add("Status="+Filter.Status.Value);
            if(Filter.DateFrom.HasValue)q.Add("DateFrom="+Filter.DateFrom.Value.ToString("yyyy-MM-dd"));
            if(Filter.DateTo.HasValue)q.Add("DateTo="+Filter.DateTo.Value.ToString("yyyy-MM-dd"));
            q.Add("PageSize=200");
            var r=await _apiClient.GetAsync<List<AdminPaymentModel>>("admin/payments"+(q.Any()?"?"+string.Join("&",q):""));
            if(r.IsSuccess&&r.Data!=null)Items=r.Data;
            else ErrorMessage=r.Message;
        }
    }
}
