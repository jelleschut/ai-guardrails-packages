using AiGuardrails.Trace;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiGuardrails.Trace.Tests;

public class CompositeTraceSinkTests
{
    [Fact]
    public async Task Keeps_going_when_one_sink_throws()
    {
        var ok = new MemorySink();
        var composite = new CompositeTraceSink([new ThrowingSink(), ok], NullLogger<CompositeTraceSink>.Instance);
        await composite.WriteAsync(TraceRecord.Start("c"));
        Assert.Equal("c", ok.Last!.CorrelationId);
    }

    [Fact]
    public async Task Writes_to_every_sink_in_order()
    {
        var a = new MemorySink();
        var b = new MemorySink();
        var composite = new CompositeTraceSink([a, b], NullLogger<CompositeTraceSink>.Instance);
        await composite.WriteAsync(TraceRecord.Start("c"));
        Assert.Equal("c", a.Last!.CorrelationId);
        Assert.Equal("c", b.Last!.CorrelationId);
    }

    private sealed class ThrowingSink : ITraceSink
    {
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }

    private sealed class MemorySink : ITraceSink
    {
        public TraceRecord? Last;
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) { Last = r; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Cancelled_token_stops_silently_without_throwing()
    {
        var a = new MemorySink();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var composite = new CompositeTraceSink([a], NullLogger<CompositeTraceSink>.Instance);
        await composite.WriteAsync(TraceRecord.Start("c"), cts.Token);
        Assert.Null(a.Last);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositeTraceSink(null!, NullLogger<CompositeTraceSink>.Instance));
        Assert.Throws<ArgumentNullException>(() => new CompositeTraceSink([], null!));
    }
}
