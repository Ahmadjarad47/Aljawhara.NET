using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecom.API.Controllers;

[ApiController]
[Route("api/admin/shipping")]
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminShippingController(IShippingService shipping) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ShippingSettingsDto>> Get(CancellationToken cancellationToken)
        => Ok(await shipping.GetSettingsAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ShippingSettingsDto>> Update(ShippingSettingsDto settings, CancellationToken cancellationToken)
    {
        try { return Ok(await shipping.UpdateSettingsAsync(settings, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Shipping settings changed. Reload before saving." });
        }
    }
}
