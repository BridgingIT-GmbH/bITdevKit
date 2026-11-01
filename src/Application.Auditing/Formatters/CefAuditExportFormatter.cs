// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Optional cef-v1 projection: one root event and one event per retained action, without Syslog or delivery claims.</summary>
/// <example><code>auditing.WithExportFormatter(_ => new CefAuditExportFormatter(options));</code></example>
public sealed class CefAuditExportFormatter : IAuditExportFormatter
{
    private readonly CefAuditExportOptions options;

    /// <summary>Validates and pins the host mapping independently of source capture.</summary>
    /// <example><code>var formatter = new CefAuditExportFormatter(options);</code></example>
    public CefAuditExportFormatter(CefAuditExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options); options.Validate(); this.options = options;
        this.Revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { options.Vendor, options.Product, options.Version, Severities = options.Severities.OrderBy(pair => pair.Key, StringComparer.Ordinal) })));
    }

    /// <inheritdoc />
    public string Format => "cef";

    /// <inheritdoc />
    public string Profile => "cef-v1";

    /// <inheritdoc />
    public string Revision { get; }

    /// <inheritdoc />
    public string MediaType => "text/plain; charset=utf-8";

    /// <inheritdoc />
    public string Extension => "cef";

    /// <inheritdoc />
    public ImmutableArray<string> Omissions => ["EntityChangeDeltas", "StructuredDetails", "SourceIntegrityEnvelope", "SyslogTransport"];

    /// <inheritdoc />
    public Task<AuditExportFrame> StartAsync(AuditExportMetadata metadata, AuditExportFormatContext context, CancellationToken cancellationToken = default) => Task.FromResult<AuditExportFrame>(null);

    /// <inheritdoc />
    public Task<AuditExportFrame> EndAsync(AuditExportCompletion completion, AuditExportFormatContext context, CancellationToken cancellationToken = default) => Task.FromResult<AuditExportFrame>(null);

    /// <inheritdoc />
    public Task<AuditExportFrame> RecordAsync(AuditExportRecord source, AuditExportFormatContext context, CancellationToken cancellationToken = default)
    {
        var buffer = new AuditExportBuffer(context.GraphByteLimit);
        long losses = 0;
        try
        {
            string Field(string value, bool header = false, int limit = 1024)
            {
                var escaped = CefAuditMappingV1.Escape(value, header, limit, out var loss);
                if (loss) losses++;
                return escaped;
            }

            void Line(string key, string name, string outcome, string identity, DateTimeOffset started, Guid? parent, int? attempt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var offset = buffer.Bytes.Length;
                buffer.WriteUtf8($"CEF:0|{Field(this.options.Vendor, true, 63)}|{Field(this.options.Product, true, 63)}|{Field(this.options.Version, true, 31)}|{Field(key, true, 1023)}|{Field(name, true, 512)}|{this.options.Severities.GetValueOrDefault(outcome).ToString(CultureInfo.InvariantCulture)}|");
                buffer.WriteUtf8($"externalId={Field(identity)} act={Field(key)} outcome={Field(outcome)} start={started.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}");
                buffer.WriteUtf8($" cs1Label=AuditId cs1={source.Details.Record.AuditId:D} cs2Label=SourceStoreId cs2={source.SourceStoreId:D} cs3Label=IntegrityStatus cs3={Field(source.Details.IntegrityStatus)} cs4Label=Completeness cs4={Field(source.Details.Record.Completeness.ToString())}");
                if (source.Details.Record.Actor?.UserId is { } actor) buffer.WriteUtf8($" suser={Field(actor)}");
                if (parent is { } parentId) buffer.WriteUtf8($" cs5Label=ParentActionId cs5={parentId:D}");
                if (attempt is { } number) buffer.WriteUtf8($" cn1Label=Attempt cn1={number.ToString(CultureInfo.InvariantCulture)}");
                buffer.WriteByte((byte)'\n');
                if (buffer.Bytes.Length - offset > 16_384) throw new AuditExportException("AuditExportCefLineTooLarge");
            }

            var record = source.Details.Record;
            Line(record.Key, record.DisplayName, record.Outcome.ToString(), $"{source.SourceStoreId:D}/{record.AuditId:D}/root", record.StartedUtc, null, null);
            foreach (var action in record.Actions) Line(action.Key, action.DisplayName, action.Outcome.ToString(), $"{source.SourceStoreId:D}/{record.AuditId:D}/{action.Id:D}", action.StartedUtc, action.ParentActionId, action.Attempt);
            return Task.FromResult(AuditExportFrame.FromBuffer(buffer, 1 + record.Actions.Length, transformations: losses == 0 ? null : new Dictionary<string, long> { ["CefFieldLoss"] = losses }));
        }
        catch { buffer.Dispose(); throw; }
    }
}
