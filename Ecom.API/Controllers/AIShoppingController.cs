using Ecom.API.AIShopping;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Application;
using Ecom.AIShopping.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ecom.API.Controllers;

[ApiController]
[Route("api/ai-shopping")]
[ServiceFilter(typeof(ShoppingRequestFilter))]
[RequestSizeLimit(16384)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AIShoppingController : ControllerBase
{
    private string Owner => (string)HttpContext.Items["AIShopping.Owner"]!;
    private CancellationToken Cancellation => (CancellationToken)HttpContext.Items["AIShopping.Cancellation"]!;

    [HttpPost("sessions")]
    public ActionResult<object> Session([FromServices] ShoppingIdentity identity) =>
        Ok(new { token = identity.CreateGuestToken(), header = ShoppingIdentity.Header, expiresInSeconds = 86400 });

    [HttpPost("chat")]
    [ProducesResponseType<ShoppingResponse>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(429)]
    public async Task<ActionResult<ShoppingResponse>> Chat(ChatRequest request,
        [FromServices] AIShoppingAgent agent) => Ok(await agent.ChatAsync(Owner, request, Cancellation));

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ShoppingResponse>> Conversation(Guid id,
        [FromServices] AIShoppingAgent agent) => Ok((await agent.GetStateAsync(id, Owner, Cancellation)).Response);

    [HttpPost("conversations/{id:guid}/recalculate")]
    public async Task<ActionResult<ShoppingResponse>> Recalculate(Guid id, RecalculateRequest request,
        [FromServices] AIShoppingAgent agent) => Ok(await agent.RecalculateAsync(id, Owner, request, Cancellation));

    [HttpPost("conversations/{id:guid}/recommendations/{recommendationId:guid}/confirm")]
    [ProducesResponseType<CartConfirmation>(200)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<ActionResult<CartConfirmation>> Confirm(Guid id, Guid recommendationId, ConfirmRequest request,
        [FromServices] IShoppingConfirmationService confirmations) =>
        Ok(await confirmations.ConfirmAsync(id, recommendationId, Owner, request, Cancellation));

    [Authorize(Roles = "Admin")]
    [HttpPost("index/batch")]
    public async Task<ActionResult<global::Ecom.AIShopping.Infrastructure.BackgroundJobs.IndexBatchResult>> IndexBatch(
        [FromQuery] bool resetCheckpoint,
        [FromServices] global::Ecom.AIShopping.Infrastructure.BackgroundJobs.ShoppingSearchIndexService index)
        => Ok(await index.RunBatchAsync(resetCheckpoint, Cancellation));

    [HttpPost("conversations/{id:guid}/recommendations/{recommendationId:guid}/prepare-cart")]
    [ProducesResponseType<PreparedBrowserCart>(200)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<PreparedBrowserCart>> PrepareBrowserCart(Guid id, Guid recommendationId,
        PrepareBrowserCartRequest request, [FromServices] BrowserCartPreparationService preparation) =>
        Ok(await preparation.PrepareAsync(id, recommendationId, Owner, request, Cancellation));
}
