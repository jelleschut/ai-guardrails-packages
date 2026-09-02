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

    [Fact]
    public void Partition_is_utc_and_daily_by_default()
    {
        var ts = new DateTimeOffset(2026, 8, 29, 23, 30, 0, TimeSpan.FromHours(-2));   // 30-08 01:30 UTC
        Assert.Equal("2026/08/30.jsonl", Sink().PartitionBlobName(ts));
    }

    [Fact]
    public void Partition_format_is_configurable()
    {
        var ts = new DateTimeOffset(2026, 8, 29, 13, 5, 0, TimeSpan.Zero);
        Assert.Equal("2026/08/29/13.jsonl", Sink(new BlobTraceSinkOptions(PartitionFormat: "yyyy'/'MM'/'dd'/'HH")).PartitionBlobName(ts));
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new BlobTraceSink(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Sink().WriteAsync(null!));
    }
}
