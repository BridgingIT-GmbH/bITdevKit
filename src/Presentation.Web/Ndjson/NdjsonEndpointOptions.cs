// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using System.Text.Json;

/// <summary>Optional endpoint overrides. Omitted values inherit server defaults.</summary>
/// <example><code>endpoint.WithNdjson&lt;Weather&gt;(o => o.Mode = NdjsonMode.Envelope);</code></example>
public sealed class NdjsonEndpointOptions
{
    /// <summary>Maximum JSON bytes, excluding delimiters. Null inherits the configured default.</summary>
    /// <example><code>options.MaxRecordBytes = 65536;</code></example>
    public int? MaxRecordBytes { get; set; }

    /// <summary>Declared raw or envelope wire mode. Null inherits the configured default.</summary>
    /// <example><code>options.Mode = NdjsonMode.Envelope;</code></example>
    public NdjsonMode? Mode { get; set; }

    /// <summary>Idle envelope activity interval. Null inherits the configured default.</summary>
    /// <example><code>options.HeartbeatInterval = TimeSpan.FromSeconds(15);</code></example>
    public TimeSpan? HeartbeatInterval { get; set; }

    /// <summary>Deadline for each response start, write, or flush. Null inherits the configured default.</summary>
    /// <example><code>options.WriteTimeout = TimeSpan.FromSeconds(30);</code></example>
    public TimeSpan? WriteTimeout { get; set; }

    /// <summary>One asynchronous producer cleanup grace period. Null inherits the configured default.</summary>
    /// <example><code>options.CleanupTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan? CleanupTimeout { get; set; }

    /// <summary>Independent terminal observer wait budget. Null inherits the configured default.</summary>
    /// <example><code>options.ObserverTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan? ObserverTimeout { get; set; }

    /// <summary>Optional overall normal-production deadline. Null inherits the configured default.</summary>
    /// <example><code>options.MaxDuration = TimeSpan.FromMinutes(10);</code></example>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>Optional framed-byte batch capacity, paired with a delay. Null inherits the configured default.</summary>
    /// <example><code>options.BatchByteLimit = 65536;</code></example>
    public int? BatchByteLimit { get; set; }

    /// <summary>Maximum delay from the first pending batch frame. Null inherits the configured default.</summary>
    /// <example><code>options.BatchMaxDelay = TimeSpan.FromMilliseconds(100);</code></example>
    public TimeSpan? BatchMaxDelay { get; set; }

    /// <summary>Explicit host compression opt-in requiring delivery verification. Null inherits the configured default.</summary>
    /// <example><code>options.EnableCompression = true;</code></example>
    public bool? EnableCompression { get; set; }

    /// <summary>Optional attachment filename; omitted for ordinary SPA consumption. Null inherits the configured default.</summary>
    /// <example><code>options.DownloadFileName = "weather.ndjson";</code></example>
    public string DownloadFileName { get; set; }

    /// <summary>Optional application JSON overrides, cloned before use. Null inherits the configured default.</summary>
    /// <example><code>options.JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);</code></example>
    public JsonSerializerOptions JsonOptions { get; set; }

    /// <summary>Explicitly removes an inherited overall duration cap.</summary>
    /// <example><code>options.ClearMaxDuration = true;</code></example>
    public bool ClearMaxDuration { get; set; }

    /// <summary>Explicitly removes inherited batching.</summary>
    /// <example><code>options.DisableBatching = true;</code></example>
    public bool DisableBatching { get; set; }

    internal NdjsonEndpointOptions Capture()
    {
        var copy = (NdjsonEndpointOptions)this.MemberwiseClone();
        if (this.JsonOptions is not null) { copy.JsonOptions = new JsonSerializerOptions(this.JsonOptions); }

        return copy;
    }
}
