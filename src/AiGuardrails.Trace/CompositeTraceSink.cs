using Microsoft.Extensions.Logging;

namespace AiGuardrails.Trace;

/// <summary>Schrijft naar alle sinks, in volgorde. Een falende sink wordt gelogd en overgeslagen: tracing mag het antwoord aan de
/// gebruiker nooit blokkeren. Een geannuleerd request is geen sink-storing: de lus stopt dan stil, zonder LogError en zonder te gooien.</summary>
public sealed class CompositeTraceSink : ITraceSink
{
    private readonly ITraceSink[] _sinks;
    private readonly ILogger<CompositeTraceSink> _log;

    public CompositeTraceSink(IEnumerable<ITraceSink> sinks, ILogger<CompositeTraceSink> log)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        ArgumentNullException.ThrowIfNull(log);
        _sinks = [.. sinks];
        _log = log;
    }

    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        foreach (var s in _sinks)
        {
            if (ct.IsCancellationRequested) return;
            try { await s.WriteAsync(record, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { _log.LogError(ex, "trace-sink {Sink} faalde voor {CorrelationId}", s.GetType().Name, record.CorrelationId); }
        }
    }
}
