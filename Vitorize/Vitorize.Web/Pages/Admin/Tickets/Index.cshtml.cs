using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Tickets;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Tickets {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class IndexModel : PageModel {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient)=>_apiClient=apiClient;
        public List<TicketModel> Tickets{
            get;
            set;
        }
        =new();
        public string? ErrorMessage{
            get;
            set;
        }
        public async Task OnGetAsync(){
            var r=await _apiClient.GetAsync<List<TicketModel>>("admin/tickets");
            if(r.IsSuccess&&r.Data!=null) Tickets=r.Data.OrderByDescending(x=>x.CreatedAt).ToList();
            else ErrorMessage=r.Message;
        }
    }
}
