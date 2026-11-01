// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>Explicit client limits and expected wire mode, captured at response opening.</summary>
/// <example><code>var options = new NdjsonClientOptions { ExpectedMode = NdjsonMode.Envelope };</code></example>
public sealed class NdjsonClientOptions
{
    /// <summary>The expected mode; raw permits an external server without DevKit headers.</summary>
    /// <example><code>options.ExpectedMode = NdjsonMode.Envelope;</code></example>
    public NdjsonMode ExpectedMode { get; set; }

    /// <summary>Caller-controlled strict framing limits.</summary>
    /// <example><code>options.ReaderOptions.MaxRecordBytes = 65536;</code></example>
    public NdjsonReaderOptions ReaderOptions { get; set; } = new();

    /// <summary>Application JSON settings, cloned at opening.</summary>
    /// <example><code>options.JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);</code></example>
    public JsonSerializerOptions JsonOptions { get; set; }

    /// <summary>Complete-envelope wait budget, excluding consumer processing time.</summary>
    /// <example><code>options.EnvelopeInactivityTimeout = TimeSpan.FromSeconds(45);</code></example>
    public TimeSpan EnvelopeInactivityTimeout { get; set; } = NdjsonDefaults.InputTimeout;

    /// <summary>Opt-in complete-record wait budget for raw streams.</summary>
    /// <example><code>options.RawInactivityTimeout = TimeSpan.FromMinutes(1);</code></example>
    public TimeSpan? RawInactivityTimeout { get; set; }

    /// <summary>Clock used for complete-record deadlines.</summary>
    /// <example><code>options.TimeProvider = TimeProvider.System;</code></example>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Validates and captures independent effective options.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (!Enum.IsDefined(this.ExpectedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(this.ExpectedMode));
        }

        ArgumentNullException.ThrowIfNull(this.ReaderOptions);
        ArgumentNullException.ThrowIfNull(this.TimeProvider);
        this.ReaderOptions.Validate();
        NdjsonOptionValidation.Positive(this.EnvelopeInactivityTimeout, nameof(this.EnvelopeInactivityTimeout));
        if (this.RawInactivityTimeout is { } raw)
        {
            NdjsonOptionValidation.Positive(raw, nameof(this.RawInactivityTimeout));
        }
    }

    internal NdjsonClientOptions Capture()
    {
        this.Validate();
        return new NdjsonClientOptions
        {
            ExpectedMode = this.ExpectedMode,
            ReaderOptions = this.ReaderOptions.Capture(),
            JsonOptions = new JsonSerializerOptions(this.JsonOptions ?? DefaultJsonSerializerOptions.Create()),
            EnvelopeInactivityTimeout = this.EnvelopeInactivityTimeout,
            RawInactivityTimeout = this.RawInactivityTimeout,
            TimeProvider = this.TimeProvider
        };
    }
}
