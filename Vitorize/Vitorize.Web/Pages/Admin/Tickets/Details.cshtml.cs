using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Models.Admin.Tickets;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;
namespace Vitorize.Web.Pages.Admin.Tickets {
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")] public class DetailsModel : PageModel {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _storage;
        public DetailsModel(ApiClient apiClient,IFileStorageService storage){
            _apiClient=apiClient;
            _storage=storage;
        }
        public TicketModel Ticket{
            get;
            set;
        }
        =new();
        [BindProperty] public AdminAddTicketMessageRequestModel Reply{
            get;
            set;
        }
        =new();
        [BindProperty] public IFormFile? AttachmentFile{
            get;
            set;
        }
        [TempData] public string? SuccessMessage{
            get;
            set;
        }
        [TempData] public string? ErrorMessage{
            get;
            set;
        }
        public async Task<IActionResult> OnGetAsync(Guid id){
            var r=await _apiClient.GetAsync<TicketModel>("admin/tickets/"+id);
            if(!r.IsSuccess||r.Data==null){
                TempData["ErrorMessage"]=r.Message;
                return RedirectToPage("Index");
            }
            Ticket=r.Data;
            return Page();
        }
        public async Task<IActionResult> OnPostReplyAsync(Guid id){
            string? path=null;
            if(AttachmentFile!=null&&AttachmentFile.Length>0) path=await _storage.SaveAsync(AttachmentFile,StorageFolders.Tickets);
            Reply.AttachmentPath=path;
            var r=await _apiClient.PostAsync<TicketModel>($"admin/tickets/{id}/messages",Reply);
            if(!r.IsSuccess&&path!=null) await _storage.DeleteAsync(path);
            TempData[r.IsSuccess?"SuccessMessage":"ErrorMessage"]=r.IsSuccess?"پاسخ ثبت شد.":r.Message;
            return RedirectToPage(new{
                id}
                );
            }
            public async Task<IActionResult> OnPostCloseAsync(Guid id){
                var r=await _apiClient.PostAsync<TicketModel>($"admin/tickets/{id}/close",new{
                }
                );
                TempData[r.IsSuccess?"SuccessMessage":"ErrorMessage"]=r.IsSuccess?"تیکت بسته شد.":r.Message;
                return RedirectToPage(new{
                    id}
                    );
                }
                public async Task<IActionResult> OnPostReopenAsync(Guid id){
                    var r=await _apiClient.PostAsync<TicketModel>($"admin/tickets/{id}/reopen",new{
                    }
                    );
                    TempData[r.IsSuccess?"SuccessMessage":"ErrorMessage"]=r.IsSuccess?"تیکت باز شد.":r.Message;
                    return RedirectToPage(new{
                        id}
                        );
                    }
                }
            }
