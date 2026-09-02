using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace AiGuardrails.Trace.Azure;

public sealed record BlobTraceSinkOptions(string ContainerName = "traces");

/// <summary>Eén JSON-regel per request in een append-blob per dag (<c>yyyy/MM/dd.jsonl</c>),
/// plus één blob per correlatie-id (<c>by-id/{id}.json</c>) voor het teruglezen van één trace.
/// Container en lifecycle (bijv. 90 dagen) zijn de verantwoordelijkheid van de infra van de consument.</summary>
public sealed class BlobTraceSink(BlobServiceClient blobs, BlobTraceSinkOptions? options = null) : ITraceSink, ITraceReader
{
    private readonly BlobContainerClient _container = blobs.GetBlobContainerClient((options ?? new BlobTraceSinkOptions()).ContainerName);

    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        CorrelationId.EnsureValid(record.CorrelationId);
        var line = JsonSerializer.Serialize(record, TraceRecord.JsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);

        var daily = _container.GetAppendBlobClient(record.Timestamp.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture) + ".jsonl");
        await daily.CreateIfNotExistsAsync(cancellationToken: ct);
        await daily.AppendBlockAsync(new MemoryStream(bytes), cancellationToken: ct);

        await _container.GetBlobClient($"by-id/{record.CorrelationId}.json")
            .UploadAsync(BinaryData.FromBytes(bytes), overwrite: true, ct);
    }

    public async Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default)
    {
        if (!CorrelationId.IsValid(correlationId)) return null;
        try
        {
            var r = await _container.GetBlobClient($"by-id/{correlationId}.json").DownloadContentAsync(ct);
            return JsonSerializer.Deserialize<TraceRecord>(r.Value.Content, TraceRecord.JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }
}
