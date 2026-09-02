using System.Diagnostics.CodeAnalysis;

namespace AiGuardrails.Trace;

/// <summary>Het correlatie-id vormt bij sinks de blob-/bestandsnaam; daarom alleen 32 lowercase hex-tekens,
/// zodat een aanroeper nooit een ander object kan raken.</summary>
public static class CorrelationId
{
    public static string New() => Guid.NewGuid().ToString("n");

    public static bool IsValid([NotNullWhen(true)] string? id) => id is { Length: 32 } && id.All(char.IsAsciiHexDigitLower);

    /// <summary>Gooit <see cref="ArgumentException"/> als het id niet aan het formaat voldoet.</summary>
    public static void EnsureValid(string? id, string paramName = "correlationId")
    {
        if (!IsValid(id)) throw new ArgumentException("correlationId moet 32 lowercase hex-tekens zijn.", paramName);
    }
}
