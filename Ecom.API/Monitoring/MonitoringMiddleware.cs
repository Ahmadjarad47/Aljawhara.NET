using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ecom.API.Monitoring;

public sealed class MonitoringMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, MonitoringState state,
        ILogger<MonitoringMiddleware> logger, IOptions<MonitoringOptions> options)
    {
        var start = Stopwatch.GetTimestamp();
        int? failedStatus = null;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            failedStatus = 499;
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            failedStatus = 500;
            logger.LogError(ex, "Unhandled request exception. TraceId={TraceId}", context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500,
                traceId = context.TraceIdentifier }, context.RequestAborted);
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var status = failedStatus ?? context.Response.StatusCode;
            // Route templates never include customer IDs, emails, query strings or tokens.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var level = status >= 500 ? LogLevel.Error : status >= 400 || duration >= options.Value.SlowRequestMilliseconds
                ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level, "HTTP {Method} {Route} returned {StatusCode} in {DurationMs} ms",
                context.Request.Method, route, status, Math.Round(duration, 1));
            // Polling the dashboard must not inflate store traffic or hide real errors.
            if (!context.Request.Path.StartsWithSegments("/api/admin/monitoring") &&
                !context.Request.Path.StartsWithSegments("/admin/monitoring") &&
                !context.Request.Path.StartsWithSegments("/monitoring") && context.Request.Path != "/health")
                state.RecordRequest(status, duration);
        }
    }
}

public sealed class MonitoringHttpHandler(ILogger<MonitoringHttpHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "Outbound HTTP {Method} to {Host} returned {StatusCode} in {DurationMs} ms",
                request.Method.Method, request.RequestUri?.Host, (int)response.StatusCode,
                Math.Round(Stopwatch.GetElapsedTime(start).TotalMilliseconds, 1));
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbound HTTP {Method} to {Host} failed in {DurationMs} ms",
                request.Method.Method, request.RequestUri?.Host, Math.Round(Stopwatch.GetElapsedTime(start).TotalMilliseconds, 1));
            throw;
        }
    }
}
