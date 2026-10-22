using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ecom.Application.WhatsApp;

public static class InvoiceFailureLog
{
    public static void Write(ILogger logger, int invoiceId, Exception exception)
    {
        // Provider/browser/SQL exception messages and Data can contain tokens, HTML or customer data.
        // Preserve original exception types, HResults and ALL managed frames, including inner errors,
        // without passing the raw exception to a logger or including machine/source file paths.
        var details = new StringBuilder();
        Append(exception, details);
        logger.LogError(new EventId(6101, "InvoicePreparationFailed"),
            "WhatsApp invoice {InvoiceId} failed. Original exception types and stack traces: {FailureDetails}",
            invoiceId, details.ToString());
    }

    private static void Append(Exception ex, StringBuilder details)
    {
        details.Append(ex.GetType().FullName).Append(" HResult=").Append(ex.HResult).AppendLine();
        details.AppendLine(new StackTrace(ex, false).ToString());
        if (ex is AggregateException aggregate)
            foreach (var inner in aggregate.InnerExceptions) Append(inner, details);
        else if (ex.InnerException != null) Append(ex.InnerException, details);
    }
}
