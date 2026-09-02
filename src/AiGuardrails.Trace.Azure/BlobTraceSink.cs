using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace AiGuardrails.Trace.Azure;

/// <summary>Instellingen voor <see cref="BlobTraceSink"/>.</summary>
/// <param name="ContainerName">Container waarin de traces staan; de sink maakt hem niet aan.</param>
/// <param name="PartitionFormat">Datumformaat (UTC) van het verzamelblob; standaard per dag. Een append-blob kan maximaal
/// 50.000 blokken bevatten, dus boven ~50.000 traces per dag kies je een fijnere indeling, bijv. <c>yyyy'/'MM'/'dd'/'HH</c>.</param>
public sealed record BlobTraceSinkOptions(string ContainerName = "traces", string PartitionFormat = "yyyy'/'MM'/'dd");

/// <summary>Eén JSON-regel per request in een append-blob per partitie (standaard per UTC-dag, <c>yyyy/MM/dd.jsonl</c>),
/// plus één blob per correlatie-id (<c>by-id/{id}.json</c>) voor het teruglezen van één trace.
/// Container en lifecycle (bijv. 90 dagen) zijn de verantwoordelijkheid van de infra van de consument.
/// Let op het Azure-plafond van 50.000 blokken per append-blob: boven ~50.000 traces per partitie faalt elke volgende append
/// (409 BlockCountExceedsLimit); kies dan een fijnere <see cref="BlobTraceSinkOptions.PartitionFormat"/>.</summary>
public sealed class BlobTraceSink : ITraceSink, ITraceReader
{
    private readonly BlobContainerClient _container;
    private readonly string _partitionFormat;

    public BlobTraceSink(BlobServiceClient blobs, BlobTraceSinkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(blobs);
        var o = options ?? new BlobTraceSinkOptions();
        _container = blobs.GetBlobContainerClient(o.ContainerName);
        _partitionFormat = o.PartitionFormat;
    }

    /// <summary>Blobnaam van de partitie waarin een record terechtkomt (UTC).</summary>
    public string PartitionBlobName(DateTimeOffset timestamp)
        => timestamp.UtcDateTime.ToString(_partitionFormat, CultureInfo.InvariantCulture) + ".jsonl";

    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        CorrelationId.EnsureValid(record.CorrelationId);
        var line = JsonSerializer.Serialize(record, TraceRecord.JsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);

        var daily = _container.GetAppendBlobClient(PartitionBlobName(record.Timestamp));
        await daily.CreateIfNotExistsAsync(cancellationToken: ct);
        using (var stream = new MemoryStream(bytes))
            await daily.AppendBlockAsync(stream, cancellationToken: ct);

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
