using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin;

[ApiController, Authorize(Policy = "AdminOnly"), Route("api/admin/product-tags")]
public sealed class AdminProductTagsController(IAdminProductTagService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResult<List<AdminProductTagDto>>>> GetAll() =>
        Ok(ApiResult<List<AdminProductTagDto>>.Success(await service.GetAllAsync()));

    [HttpPost]
    public async Task<ActionResult<ApiResult<AdminProductTagDto>>> Create(SaveProductTagRequestDto request) =>
        Ok(ApiResult<AdminProductTagDto>.Success(await service.CreateAsync(request)));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResult<AdminProductTagDto>>> Update(Guid id, SaveProductTagRequestDto request) =>
        Ok(ApiResult<AdminProductTagDto>.Success(await service.UpdateAsync(id, request)));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResult>> Delete(Guid id)
    {
        await service.DeleteAsync(id);
        return Ok(ApiResult.Success());
    }
}
