// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Pins raw NDJSON metadata, record and completion entries, with no generic streaming envelope.</summary>
/// <example><code>var value = new AuditExportEntry();</code></example>
public sealed record AuditExportEntry
{
    /// <summary>metadata, record, or completion.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Pinned export representation version.</summary>
    /// <example><code>var value = snapshot.ExportSchemaVersion;</code></example>
    public int ExportSchemaVersion { get; init; } = 1;

    /// <summary>Bounded initial source and selection metadata.</summary>
    /// <example><code>var value = snapshot.Metadata;</code></example>
    public AuditExportMetadata Metadata { get; init; }

    /// <summary>One complete permitted source graph and original-boundary references.</summary>
    /// <example><code>var value = snapshot.Record;</code></example>
    public AuditExportRecord Record { get; init; }

    /// <summary>Tentative in-band terminal facts requiring successful final output and companion confirmation.</summary>
    /// <example><code>var value = snapshot.Completion;</code></example>
    public AuditExportCompletion Completion { get; init; }
}
