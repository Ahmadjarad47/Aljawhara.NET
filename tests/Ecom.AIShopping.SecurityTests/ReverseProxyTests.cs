using System.Net;
using Ecom.API.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ecom.AIShopping.SecurityTests;

public sealed class ReverseProxyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task Trusted_loopback_proxy_recovers_one_client_hop_and_https(string proxy)
    {
        var context = Create(proxy, "203.0.113.9, 198.51.100.8");
        await Middleware().Invoke(context);
        Assert.Equal(IPAddress.Parse("198.51.100.8"), context.Connection.RemoteIpAddress);
        Assert.True(context.Request.IsHttps);
    }

    [Theory]
    [InlineData("203.0.113.7")]
    [InlineData("127.0.0.2")]
    public async Task Untrusted_proxy_cannot_spoof_identity_or_https(string proxy)
    {
        var context = Create(proxy, "198.51.100.8");
        await Middleware().Invoke(context);
        Assert.Equal(IPAddress.Parse(proxy), context.Connection.RemoteIpAddress);
        Assert.False(context.Request.IsHttps);
    }

    private static DefaultHttpContext Create(string proxy, string forwarded)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = forwarded;
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        return context;
    }

    private static ForwardedHeadersMiddleware Middleware()
    {
        var options = new ForwardedHeadersOptions();
        TrustedReverseProxy.Configure(options);
        return new(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
    }
}
