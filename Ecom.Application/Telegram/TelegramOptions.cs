using System.Text.RegularExpressions;

namespace Ecom.Application.Telegram;

public sealed class TelegramOptions
{
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public long? MessageThreadId { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Kuwait";
    public int MaxAttempts { get; set; } = 8;
    public bool ProtectContent { get; set; }

    public bool IsValid()
    {
        if (!Enabled) return true;
        return Regex.IsMatch(BotToken, "^[0-9]+:[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant) &&
               !string.IsNullOrWhiteSpace(ChatId) && ChatId.Length <= 100 &&
               MaxAttempts is >= 1 and <= 20 &&
               !string.IsNullOrWhiteSpace(TimeZoneId);
    }
}
