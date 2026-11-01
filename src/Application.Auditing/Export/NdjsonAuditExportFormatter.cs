// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Encodes raw metadata, record and completion entries once using the shared owned-frame encoder.</summary>
/// <example><code>var formatter = new NdjsonAuditExportFormatter();</code></example>
public sealed class NdjsonAuditExportFormatter : IAuditExportFormatter
{
    /// <inheritdoc />
    public string Format => "ndjson";

    /// <inheritdoc />
    public string Profile => "ndjson-v1";

    /// <inheritdoc />
    public string MediaType => "application/x-ndjson";

    /// <inheritdoc />
    public string Extension => "ndjson";

    /// <inheritdoc />
    public ImmutableArray<string> Omissions => ["OriginalSourceEnvelopeOmitted", "RetainedUnknownSourceFieldsOmitted"];

    /// <inheritdoc />
    public Task<AuditExportFrame> StartAsync(AuditExportMetadata metadata, AuditExportFormatContext context, CancellationToken cancellationToken = default)
        => EncodeAsync(new() { Kind = "metadata", Metadata = metadata }, context.MetadataByteLimit, 0, false, cancellationToken);

    /// <inheritdoc />
    public Task<AuditExportFrame> RecordAsync(AuditExportRecord record, AuditExportFormatContext context, CancellationToken cancellationToken = default)
        => EncodeAsync(new() { Kind = "record", Record = record }, context.GraphByteLimit, 1, true, cancellationToken);

    /// <inheritdoc />
    public Task<AuditExportFrame> EndAsync(AuditExportCompletion completion, AuditExportFormatContext context, CancellationToken cancellationToken = default)
        => EncodeAsync(new() { Kind = "completion", Completion = completion }, context.CompletionByteLimit, 0, false, cancellationToken);

    private static async Task<AuditExportFrame> EncodeAsync(AuditExportEntry entry, int maximumBytes, int items, bool data, CancellationToken cancellationToken)
    {
        try
        {
            var writer = new NdjsonWriter(AuditExportJson.CreateOptions(), new() { MaxRecordBytes = maximumBytes - 1 });
            var frame = await writer.EncodeRecordAsync(entry, cancellationToken).ConfigureAwait(false);
            if (frame.FramedBytes.Length > maximumBytes) { frame.Dispose(); throw new AuditExportException("AuditExportRecordTooLarge"); }

            return AuditExportFrame.FromJson(frame, items, data);
        }
        catch (NdjsonStreamException) { throw new AuditExportException("AuditExportRecordTooLarge"); }
    }
}
