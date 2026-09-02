using Microsoft.Extensions.Logging;

namespace AiGuardrails.Trace;

/// <summary>Schrijft naar alle sinks, in volgorde; een falende sink mag het antwoord aan de gebruiker niet blokkeren.</summary>
public sealed class CompositeTraceSink(IEnumerable<ITraceSink> sinks, ILogger<CompositeTraceSink> log) : ITraceSink
{
    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        foreach (var s in sinks)
        {
            try { await s.WriteAsync(record, ct); }
            catch (Exception ex) { log.LogError(ex, "trace-sink {Sink} faalde voor {CorrelationId}", s.GetType().Name, record.CorrelationId); }
        }
    }
}
