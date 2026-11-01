// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>Immutable mode and record declaration shared by runtime execution and metadata.</summary>
/// <example><code>var descriptor = httpContext.GetEndpoint().Metadata.GetMetadata&lt;NdjsonEndpointDescriptor&gt;();</code></example>
public sealed class NdjsonEndpointDescriptor
{
    internal NdjsonEndpointDescriptor(Type recordType, NdjsonStreamingOptions settings, bool isExplicit = true)
    {
        this.RecordType = recordType;
        this.IsExplicit = isExplicit;
        this.Settings = settings.Capture();
    }

    /// <summary>The per-record application model, never a JSON response array.</summary>
    /// <example><code>var type = descriptor.RecordType;</code></example>
    public Type RecordType { get; }

    /// <summary>The fixed endpoint mode.</summary>
    /// <example><code>var mode = descriptor.Mode;</code></example>
    public NdjsonMode Mode => this.Settings.Mode;

    /// <summary>The endpoint's explicit compression opt-in.</summary>
    /// <example><code>var compressed = descriptor.EnableCompression;</code></example>
    public bool EnableCompression => this.Settings.EnableCompression;

    internal bool IsExplicit { get; }
    internal NdjsonStreamingOptions Settings { get; }
}
