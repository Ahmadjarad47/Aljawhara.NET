using System.Security.Claims;
using Ecom.Application.DTOs.Auth;
using Ecom.Application.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers;

[ApiController]
[Authorize]
[Route("api/account/whatsapp")]
public sealed class WhatsAppPreferencesController(WhatsAppConsentService consent) : ControllerBase
{
    [HttpGet("preferences")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var preference = await consent.GetAsync(UserId(), ct);
        return preference == null ? NotFound() : Ok(preference);
    }

    [HttpPost("phone-verification")]
    public async Task<IActionResult> RequestVerification(RequestWhatsAppPhoneVerificationDto request, CancellationToken ct)
    {
        try
        {
            var result = await consent.RequestPhoneVerificationAsync(UserId(), request.PhoneNumber, ct);
            return result.Accepted
                ? Accepted(new { message = "Verification code sent." })
                : StatusCode(StatusCodes.Status502BadGateway, new { message = "Phone verification could not be sent.", code = result.ErrorCode });
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("phone-verification/confirm")]
    public async Task<IActionResult> ConfirmVerification(ConfirmWhatsAppPhoneVerificationDto request, CancellationToken ct)
    {
        try { return Ok(await consent.ConfirmPhoneAsync(UserId(), request.PhoneNumber, request.Code, ct)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> Update(UpdateWhatsAppPreferenceDto request, CancellationToken ct)
    {
        try { return Ok(await consent.UpdatePreferenceAsync(UserId(), request.Enabled, request.PhoneNumber, ct)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user identifier is missing.");
}
