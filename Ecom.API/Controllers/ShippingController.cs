using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers;

[ApiController]
[Route("api/shipping")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ShippingController(IShippingService shipping) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ShippingSettingsDto>> Get(CancellationToken cancellationToken)
        => Ok(await shipping.GetSettingsAsync(cancellationToken));
}
