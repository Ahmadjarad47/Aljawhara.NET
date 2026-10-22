using Ecom.Application.WhatsApp;
using Microsoft.Extensions.Logging;

namespace Ecom.CheckoutAudit.Tests;

public sealed class InvoiceFailureLogTests
{
    [Fact]
    public void Original_exception_and_inner_stacks_are_preserved_without_payloads()
    {
        var logger = new CaptureLogger();
        try { Outer(); }
        catch (Exception ex) { InvoiceFailureLog.Write(logger, 6, ex); }
        Assert.Contains("System.InvalidOperationException", logger.Text);
        Assert.Contains("System.IO.IOException", logger.Text);
        Assert.Contains(nameof(Inner), logger.Text);
        Assert.Contains(nameof(Outer), logger.Text);
        Assert.Contains("6", logger.Text);
        Assert.DoesNotContain("SECRET", logger.Text);
        Assert.DoesNotContain("CUSTOMER", logger.Text);
        Assert.Null(logger.Exception);
    }

    private static void Inner() => throw new IOException("SECRET token=abcd CUSTOMER name phone HTML");
    private static void Outer()
    {
        try { Inner(); }
        catch (Exception ex) { throw new InvalidOperationException("CUSTOMER and SECRET", ex); }
    }

    private sealed class CaptureLogger : ILogger
    {
        public string Text { get; private set; } = "";
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { Text = formatter(state, exception); Exception = exception; }
    }
}
