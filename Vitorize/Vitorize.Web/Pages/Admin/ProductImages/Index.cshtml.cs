using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.ProductImages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;
namespace Vitorize.Web.Pages.Admin.ProductImages
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;
        public IndexModel(ApiClient apiClient, IFileStorageService fileStorage) {
            _apiClient = apiClient;
            _fileStorage = fileStorage;
        }
        [BindProperty(SupportsGet = true)] public Guid? ProductId {
            get;
            set;
        }
        public List<AdminProductModel> Products {
            get;
            set;
        }
        = new();
        public AdminProductModel? Product {
            get;
            set;
        }
        public List<AdminProductImageModel> Images {
            get;
            set;
        }
        = new();
        [BindProperty] public IFormFile? ImageFile {
            get;
            set;
        }
        [BindProperty] public string? AltText {
            get;
            set;
        }
        [BindProperty] public int SortOrder {
            get;
            set;
        }
        = 10;
        [BindProperty] public bool SetAsThumbnail {
            get;
            set;
        }
        [TempData] public string? SuccessMessage {
            get;
            set;
        }
        [TempData] public string? ErrorMessage {
            get;
            set;
        }
        public async Task OnGetAsync() => await LoadAsync();
        public async Task<IActionResult> OnPostUploadAsync(Guid productId)
        {
            ProductId = productId;
            await LoadAsync();
            if (productId == Guid.Empty || Product == null) {
                ErrorMessage = "محصول انتخاب نشده است.";
                return Page();
            }
            if (ImageFile == null || ImageFile.Length == 0) {
                ErrorMessage = "تصویر را انتخاب کن.";
                return Page();
            }
            string? newPath = null;
            try
            {
                newPath = await _fileStorage.SaveAsync(ImageFile, StorageFolders.Products);
                var request = new CreateProductImageRequestModel {
                    ImagePath = newPath ?? string.Empty, AltText = string.IsNullOrWhiteSpace(AltText) ? Product.Title : AltText, SortOrder = SortOrder, SetAsThumbnail = SetAsThumbnail || !Images.Any() }
                    ;
                    var result = await _apiClient.PostAsync<AdminProductImageModel>($"admin/products/{productId}/images", request);
                    if (!result.IsSuccess) {
                        if (!string.IsNullOrWhiteSpace(newPath)) await _fileStorage.DeleteAsync(newPath);
                        ErrorMessage = result.Message;
                        return Page();
                    }
                    TempData["SuccessMessage"] = "تصویر با موفقیت ثبت شد.";
                    return RedirectToPage(new {
                        productId }
                        );
                    }
                    catch (Exception ex)
                    {
                        if (!string.IsNullOrWhiteSpace(newPath)) await _fileStorage.DeleteAsync(newPath);
                        ErrorMessage = ex.Message;
                        return Page();
                    }
                }
                public async Task<IActionResult> OnPostUpdateAsync(Guid productId, Guid imageId, string imagePath, string? altText, int sortOrder)
                {
                    var result = await _apiClient.PutAsync<AdminProductImageModel>($"admin/product-images/{imageId}", new UpdateProductImageRequestModel {
                        ImagePath = imagePath, AltText = altText, SortOrder = sortOrder }
                        );
                        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "تصویر با موفقیت ویرایش شد." : result.Message;
                        return RedirectToPage(new {
                            productId }
                            );
                        }
                        public async Task<IActionResult> OnPostSetThumbnailAsync(Guid productId, Guid imageId)
                        {
                            var result = await _apiClient.PostAsync<object>($"admin/product-images/{imageId}/set-thumbnail", new {
                            }
                            );
                            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "تصویر اصلی تغییر کرد." : result.Message;
                            return RedirectToPage(new {
                                productId }
                                );
                            }
                            public async Task<IActionResult> OnPostDeleteAsync(Guid productId, Guid imageId)
                            {
                                await LoadImagesAsync(productId);
                                var current = Images.FirstOrDefault(x => x.Id == imageId);
                                var result = await _apiClient.DeleteAsync($"admin/product-images/{imageId}");
                                if (result.IsSuccess && current != null) await _fileStorage.DeleteAsync(current.ImagePath);
                                TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "تصویر حذف شد." : result.Message;
                                return RedirectToPage(new {
                                    productId }
                                    );
                                }
                                private async Task LoadAsync()
                                {
                                    var productsResult = await _apiClient.GetAsync<List<AdminProductModel>>("admin/products");
                                    Products = productsResult.IsSuccess && productsResult.Data != null ? productsResult.Data.OrderBy(x => x.Title).ToList() : new();
                                    if (ProductId.HasValue && ProductId.Value != Guid.Empty)
                                    {
                                        Product = Products.FirstOrDefault(x => x.Id == ProductId.Value) ?? (await _apiClient.GetAsync<AdminProductModel>("admin/products/" + ProductId.Value)).Data;
                                        await LoadImagesAsync(ProductId.Value);
                                    }
                                }
                                private async Task LoadImagesAsync(Guid productId)
                                {
                                    var result = await _apiClient.GetAsync<List<AdminProductImageModel>>($"admin/products/{productId}/images");
                                    Images = result.IsSuccess && result.Data != null ? result.Data.OrderByDescending(x => x.IsThumbnail).ThenBy(x => x.SortOrder).ToList() : new();
                                }
                                public string Image(string? path) => AdminUiHelper.ImageUrl(path);
                            }
                        }
