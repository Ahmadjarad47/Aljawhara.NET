using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ecom.Application.Services;

public interface ISmtpTransport
{
    Task SendAsync(MimeMessage message, string host, int port, string username, string password);
}

public sealed class SmtpTransport : ISmtpTransport
{
    public async Task SendAsync(MimeMessage message, string host, int port, string username, string password)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, timeout.Token);
        await client.AuthenticateAsync(username, password, timeout.Token);
        await client.SendAsync(message, timeout.Token);
        await client.DisconnectAsync(true, timeout.Token);
    }
}
