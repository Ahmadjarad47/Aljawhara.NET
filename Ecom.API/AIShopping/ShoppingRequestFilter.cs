using System.Threading.RateLimiting;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Ecom.API.AIShopping;

public sealed class ShoppingRequestGate(IOptionsMonitor<AIShoppingOptions> options) : IDisposable
{
    private readonly PartitionedRateLimiter<string> _rate = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = key.StartsWith("ip:", StringComparison.Ordinal) ? options.CurrentValue.IpRequestsPerMinute : options.CurrentValue.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1), AutoReplenishment = true, QueueLimit = 0
        }));
    private readonly ConcurrencyLimiter _concurrency = new(new() { PermitLimit = options.CurrentValue.MaxConcurrentRequests,
        QueueLimit = 0, QueueProcessingOrder = QueueProcessingOrder.OldestFirst });

    public RateLimitLease Enter(string ip, string owner)
    {
        using var ipLease = _rate.AttemptAcquire("ip:" + ip);
        using var ownerLease = _rate.AttemptAcquire("owner:" + owner);
        if (!ipLease.IsAcquired || !ownerLease.IsAcquired) throw new ShoppingException("AI_RATE_LIMIT", 429);
        var lease = _concurrency.AttemptAcquire();
        if (!lease.IsAcquired) { lease.Dispose(); throw new ShoppingException("AI_BUSY", 429); }
        return lease;
    }
    public void Dispose() { _rate.Dispose(); _concurrency.Dispose(); }
}

public sealed class ShoppingRequestFilter(IOptionsMonitor<AIShoppingOptions> options, ShoppingRequestGate gate,
    ShoppingIdentity identity, ILogger<ShoppingRequestFilter> logger) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            if (!options.CurrentValue.Enabled) throw new ShoppingException("AI_SHOPPING_DISABLED", 404);
            if (http.Request.ContentLength > 16384) throw new ShoppingException("REQUEST_TOO_LARGE", 413);
            var isSession = http.Request.Path.Equals("/api/ai-shopping/sessions", StringComparison.OrdinalIgnoreCase);
            var owner = isSession ? "session:" + http.Connection.RemoteIpAddress : identity.Owner(http);
            using var lease = gate.Enter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", owner);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.CurrentValue.TimeoutSeconds));
            http.Items["AIShopping.Owner"] = owner;
            http.Items["AIShopping.Cancellation"] = timeout.Token;
            var executed = await next();
            if (http.Request.Path.Equals("/api/ai-shopping/chat", StringComparison.OrdinalIgnoreCase) &&
                executed.Result is ObjectResult { Value: Ecom.AIShopping.Contracts.ShoppingResponse response })
                logger.LogInformation("AI shopping turn completed. TraceId={TraceId}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, EmbeddingTokens={EmbeddingTokens}, EstimatedUsd={EstimatedUsd}, DurationMs={DurationMs}",
                    http.TraceIdentifier, response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.EmbeddingInputTokens,
                    response.Usage.EstimatedUsd, response.Usage.DurationMs);
            if (executed.Exception != null && !executed.ExceptionHandled)
            {
                if (executed.Exception is ShoppingException known)
                { executed.Result = Error(known.Code, known.Status, http.TraceIdentifier); executed.ExceptionHandled = true; }
                else if (executed.Exception is OperationCanceledException && !http.RequestAborted.IsCancellationRequested)
                { executed.Result = Error("AI_TIMEOUT", 504, http.TraceIdentifier); executed.ExceptionHandled = true; }
                else if (!http.RequestAborted.IsCancellationRequested)
                {
                    // Do not log provider/DB exception messages: they may contain untrusted data or secrets.
                    logger.LogWarning("AI request failed. TraceId={TraceId}, Code=AI_UNAVAILABLE", http.TraceIdentifier);
                    executed.Result = Error("AI_UNAVAILABLE", 503, http.TraceIdentifier); executed.ExceptionHandled = true;
                }
                // Resource filters run around result execution; an exception result set here must be executed explicitly.
                if (executed.ExceptionHandled && executed.Result is not null)
                    await executed.Result.ExecuteResultAsync(context);
            }
        }
        catch (ShoppingException ex) { context.Result = Error(ex.Code, ex.Status, http.TraceIdentifier); }
    }

    private static ObjectResult Error(string code, int status, string trace) => new(new ProblemDetails
    { Title = code, Status = status, Extensions = { ["code"] = code, ["traceId"] = trace } }) { StatusCode = status };
}
