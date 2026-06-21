using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        [HttpGet("public")]
        public async Task<ActionResult<ApiResult<List<SettingDto>>>> GetPublic()
        {
            var result = await _settingService.GetPublicSettingsAsync();

            return Ok(ApiResult<List<SettingDto>>.Success(
                result,
                "تنظیمات عمومی با موفقیت دریافت شد."));
        }
    }
}