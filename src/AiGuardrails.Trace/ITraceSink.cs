namespace AiGuardrails.Trace;

/// <summary>Contract: implementaties horen niet te gooien; <see cref="CompositeTraceSink"/> vangt als vangrail toch af.</summary>
public interface ITraceSink
{
    Task WriteAsync(TraceRecord record, CancellationToken ct = default);
}

public interface ITraceReader
{
    Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default);
}
