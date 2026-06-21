using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/settings")]
    public class AdminSettingsController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public AdminSettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<SettingGroupDto>>>> GetAll()
        {
            var result = await _settingService.GetAllGroupedAsync();

            return Ok(ApiResult<List<SettingGroupDto>>.Success(
                result,
                "تنظیمات با موفقیت دریافت شد."));
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<ApiResult<SettingDto?>>> GetByKey(string key)
        {
            var result = await _settingService.GetByKeyAsync(key);

            return Ok(ApiResult<SettingDto?>.Success(
                result,
                "تنظیمات با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<SettingDto>>> Upsert(
            UpdateSettingDto request)
        {
            var result = await _settingService.UpsertAsync(request);

            return Ok(ApiResult<SettingDto>.Success(
                result,
                "تنظیمات با موفقیت ذخیره شد."));
        }

        [HttpDelete("{key}")]
        public async Task<ActionResult<ApiResult>> Delete(string key)
        {
            await _settingService.DeleteAsync(key);

            return Ok(ApiResult.Success(
                "تنظیمات با موفقیت حذف شد."));
        }
    }
}