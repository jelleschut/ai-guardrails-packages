using AiGuardrails.Trace;
using AiGuardrails.Trace.Azure;
using Azure.Storage.Blobs;

namespace AiGuardrails.Trace.Azure.Tests;

public class BlobTraceSinkTests
{
    private static BlobTraceSink Sink(BlobTraceSinkOptions? o = null)
        => new(new BlobServiceClient(new Uri("https://example.blob.core.windows.net")), o);

    [Fact]
    public async Task Write_rejects_invalid_correlation_id_before_any_network_call()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sink().WriteAsync(TraceRecord.Start("../by-id/x")));
        Assert.Contains("32 lowercase hex", ex.Message);
    }

    [Fact]
    public async Task Read_returns_null_for_invalid_id_without_network_call()
        => Assert.Null(await Sink().ReadAsync("NOT-HEX"));

    [Fact]
    public void Container_name_defaults_to_traces_and_is_configurable()
    {
        Assert.Equal("traces", new BlobTraceSinkOptions().ContainerName);
        Assert.Equal("ai-traces", new BlobTraceSinkOptions("ai-traces").ContainerName);
    }
}
