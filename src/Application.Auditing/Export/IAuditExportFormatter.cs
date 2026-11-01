// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;

/// <summary>Formats only verified, permitted immutable projections. It owns no selection, cursor, authorization, or source-key access.</summary>
/// <example><code>auditing.WithExportFormatter&lt;CustomerExportFormatter&gt;();</code></example>
public interface IAuditExportFormatter
{
    /// <summary>The unique case-insensitive format selector.</summary>
    /// <example><code>var format = formatter.Format;</code></example>
    string Format { get; }

    /// <summary>The pinned projection and framing profile.</summary>
    /// <example><code>var profile = formatter.Profile;</code></example>
    string Profile { get; }

    /// <summary>Host mapping configuration revision, included in continuation exposure binding.</summary>
    /// <example><code>var revision = formatter.Revision;</code></example>
    string Revision => this.Profile;

    /// <summary>The response media type without content encoding.</summary>
    /// <example><code>var mediaType = formatter.MediaType;</code></example>
    string MediaType { get; }

    /// <summary>The safe attachment extension without its dot.</summary>
    /// <example><code>var extension = formatter.Extension;</code></example>
    string Extension { get; }

    /// <summary>Declared bounded omissions. They do not imply capture or independent authenticity.</summary>
    /// <example><code>var omissions = formatter.Omissions;</code></example>
    ImmutableArray<string> Omissions { get; }

    /// <summary>Produces bounded initial framing or null when the profile has none.</summary>
    /// <example><code>using var prefix = await formatter.StartAsync(metadata, context, cancellationToken);</code></example>
    Task<AuditExportFrame> StartAsync(AuditExportMetadata metadata, AuditExportFormatContext context, CancellationToken cancellationToken = default);

    /// <summary>Encodes one complete source expansion exactly once, transferring ownership to its caller.</summary>
    /// <example><code>using var frame = await formatter.RecordAsync(record, context, cancellationToken);</code></example>
    Task<AuditExportFrame> RecordAsync(AuditExportRecord record, AuditExportFormatContext context, CancellationToken cancellationToken = default);

    /// <summary>Produces bounded terminal framing or null for data-only profiles.</summary>
    /// <example><code>using var terminal = await formatter.EndAsync(completion, context, cancellationToken);</code></example>
    Task<AuditExportFrame> EndAsync(AuditExportCompletion completion, AuditExportFormatContext context, CancellationToken cancellationToken = default);
}

/// <summary>Provides fixed formatting budgets without source services or HMAC keys.</summary>
/// <example><code>var context = new AuditExportFormatContext { GraphByteLimit = 8192 };</code></example>
public sealed record AuditExportFormatContext
{
    /// <summary>Maximum whole-source formatted bytes.</summary>
    /// <example><code>var limit = context.GraphByteLimit;</code></example>
    public int GraphByteLimit { get; init; } = 8_388_608;

    /// <summary>Reserved maximum initial-framing bytes.</summary>
    /// <example><code>var limit = context.MetadataByteLimit;</code></example>
    public int MetadataByteLimit { get; init; } = 32_768;

    /// <summary>Reserved maximum terminal-framing bytes.</summary>
    /// <example><code>var limit = context.CompletionByteLimit;</code></example>
    public int CompletionByteLimit { get; init; } = 32_768;
}
