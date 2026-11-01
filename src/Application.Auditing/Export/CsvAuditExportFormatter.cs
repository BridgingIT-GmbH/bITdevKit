// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

/// <summary>Streams fixed csv-v1 columns and CRLF records with JSON cells and explicit spreadsheet formula neutralization.</summary>
/// <example><code>var formatter = new CsvAuditExportFormatter();</code></example>
public sealed class CsvAuditExportFormatter : IAuditExportFormatter
{
    /// <summary>The fixed versioned column order.</summary>
    /// <example><code>var columns = CsvAuditExportFormatter.Columns;</code></example>
    public static ImmutableArray<string> Columns { get; } = ["SourceStoreId", "AuditId", "SchemaVersion", "StartedUtc", "CompletedUtc", "UserId", "InitiatingUserId", "ModuleName", "Key", "DisplayName", "Outcome", "Completeness", "StorageState", "IntegrityStatus", "ActionsJson", "EntityChangesJson", "DetailsJson"];

    /// <inheritdoc />
    public string Format => "csv";

    /// <inheritdoc />
    public string Profile => "csv-v1";

    /// <inheritdoc />
    public string MediaType => "text/csv";

    /// <inheritdoc />
    public string Extension => "csv";

    /// <inheritdoc />
    public ImmutableArray<string> Omissions => ["OriginalSourceEnvelopeOmitted", "RetainedUnknownSourceFieldsOmitted", "SourceHashAvailableInMetadataOnly"];

    /// <inheritdoc />
    public Task<AuditExportFrame> StartAsync(AuditExportMetadata metadata, AuditExportFormatContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new AuditExportBuffer(context.MetadataByteLimit);
        try { buffer.WriteUtf8(string.Join(',', Columns) + "\r\n"); return Task.FromResult(AuditExportFrame.FromBuffer(buffer, 0)); }
        catch { buffer.Dispose(); throw; }
    }

    /// <inheritdoc />
    public Task<AuditExportFrame> RecordAsync(AuditExportRecord source, AuditExportFormatContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new AuditExportBuffer(context.GraphByteLimit);
        try
        {
            var record = source.Details.Record;
            long neutralized = 0;
            void Cell(string text, bool numeric = false)
            {
                if (buffer.Bytes.Length > 0) buffer.WriteByte((byte)',');
                if (text is null) return;
                var significant = text.AsSpan();
                while (!significant.IsEmpty && (char.IsWhiteSpace(significant[0]) || char.IsControl(significant[0]) || significant[0] == '\uFEFF')) significant = significant[1..];
                if (!numeric && !significant.IsEmpty && significant[0] is '=' or '+' or '-' or '@')
                {
                    text = "'" + text;
                    neutralized++;
                }

                var quote = text.Length == 0 || text.IndexOfAny([',', '"', '\r', '\n']) >= 0;
                if (quote) buffer.WriteByte((byte)'"');
                var from = 0;
                for (var index = 0; index < text.Length; index++)
                {
                    if (text[index] != '"') continue;
                    buffer.WriteUtf8(text[from..index]); buffer.WriteUtf8("\"\""); from = index + 1;
                }

                buffer.WriteUtf8(text[from..]);
                if (quote) buffer.WriteByte((byte)'"');
            }

            void JsonCell<T>(T value)
            {
                buffer.WriteByte((byte)','); buffer.WriteByte((byte)'"');
                using (var writer = new Utf8JsonWriter(buffer.CsvJsonCellWriter()))
                {
                    JsonSerializer.Serialize(writer, value, AuditExportJson.CreateOptions()); writer.Flush();
                }

                buffer.WriteByte((byte)'"');
            }

            Cell(source.SourceStoreId.ToString("D")); Cell(record.AuditId.ToString("D")); Cell(record.SchemaVersion.ToString(CultureInfo.InvariantCulture), true);
            Cell(Utc(record.StartedUtc)); Cell(record.CompletedUtc is { } completed ? Utc(completed) : null);
            Cell(record.Actor?.UserId); Cell(record.InitiatingActor?.UserId); Cell(record.ModuleName); Cell(record.Key); Cell(record.DisplayName);
            Cell(record.Outcome.ToString()); Cell(record.Completeness.ToString()); Cell(source.Details.StorageState); Cell(source.Details.IntegrityStatus);
            JsonCell(record.Actions); JsonCell(record.EntityChanges); JsonCell(record.Details);
            buffer.WriteUtf8("\r\n");
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuditExportFrame.FromBuffer(buffer, 1, transformations: neutralized == 0 ? null : new Dictionary<string, long> { ["FormulaNeutralized"] = neutralized }));
        }
        catch { buffer.Dispose(); throw; }
    }

    /// <inheritdoc />
    public Task<AuditExportFrame> EndAsync(AuditExportCompletion completion, AuditExportFormatContext context, CancellationToken cancellationToken = default) => Task.FromResult<AuditExportFrame>(null);

    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
}
