namespace AIKernel.Core.Governance.ChatChain;

using System.Text.Json;
using AIKernel.Abstractions.Governance.ChatChain;

public sealed class DefaultChatTurnCanonicalizer : IChatTurnCanonicalizer
{
    public string Canonicalize(IChatTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);

        var payload = new
        {
            actor = NormalizeText(turn.Actor),
            body = NormalizeBody(turn.Body),
            timestamp_utc = NormalizeTimestamp(turn.Timestamp)
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string NormalizeBody(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeTimestamp(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Utc
            ? timestamp
            : timestamp.ToUniversalTime();

        return utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }
}
