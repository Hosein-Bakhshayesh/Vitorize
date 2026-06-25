using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Verification;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Verifications {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class DetailsModel : PageModel {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient)=>_apiClient=apiClient;
        public VerificationProfileModel Item{
            get;
            set;
        }
        =new();
        [BindProperty] public ReviewVerificationRequestModel Review{
            get;
            set;
        }
        =new();
        [TempData] public string? SuccessMessage{
            get;
            set;
        }
        [TempData] public string? ErrorMessage{
            get;
            set;
        }
        public async Task<IActionResult> OnGetAsync(Guid id){
            var r=await _apiClient.GetAsync<VerificationProfileModel>("admin/verifications/"+id);
            if(!r.IsSuccess||r.Data==null){
                TempData["ErrorMessage"]=r.Message;
                return RedirectToPage("Index");
            }
            Item=r.Data;
            return Page();
        }
        public async Task<IActionResult> OnPostReviewAsync(Guid id){
            var r=await _apiClient.PostAsync<VerificationProfileModel>($"admin/verifications/{id}/review", Review);
            TempData[r.IsSuccess?"SuccessMessage":"ErrorMessage"]=r.IsSuccess?"نتیجه بررسی ثبت شد.":r.Message;
            return RedirectToPage(new{
                id}
                );
            }
        }
    }
