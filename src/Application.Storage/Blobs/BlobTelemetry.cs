// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;

/// <summary>Identifies the activity source for named blob store clients.</summary>
/// <example><code>tracing.AddSource(BlobTelemetry.ActivitySourceName);</code></example>
public static class BlobTelemetry
{
    /// <summary>Names the activity source used by the blob tracing behavior.</summary>
    /// <example><code>var sourceName = BlobTelemetry.ActivitySourceName;</code></example>
    public const string ActivitySourceName = "BridgingIT.DevKit.Application.Storage.Blobs";

    /// <summary>Gets the shared activity source used to trace named blob client operations.</summary>
    /// <remarks>Blob Storage owns the lifetime of this source.</remarks>
    /// <example><code>var hasListeners = BlobTelemetry.Spans.HasListeners();</code></example>
    public static ActivitySource Spans { get; } = new(ActivitySourceName);
}
