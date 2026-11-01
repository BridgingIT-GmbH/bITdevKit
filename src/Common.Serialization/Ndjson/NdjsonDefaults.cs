// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Shared NDJSON wire constants and deadlines.</summary>
/// <example><code>var limit = NdjsonDefaults.MAX_RECORD_BYTES;</code></example>
public static class NdjsonDefaults
{
    /// <summary>The default maximum JSON bytes, excluding the delimiter.</summary>
    /// <example><code>var limit = NdjsonDefaults.MAX_RECORD_BYTES;</code></example>
    public const int MAX_RECORD_BYTES = 1_048_576;

    /// <summary>The NDJSON response media type.</summary>
    /// <example><code>request.Headers.Accept.ParseAdd(NdjsonDefaults.CONTENT_TYPE);</code></example>
    public const string CONTENT_TYPE = "application/x-ndjson";

    /// <summary>The default idle heartbeat interval.</summary>
    /// <example><code>var interval = NdjsonDefaults.HeartbeatInterval;</code></example>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>The default response operation deadline.</summary>
    /// <example><code>var timeout = NdjsonDefaults.WriteTimeout;</code></example>
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The default producer cleanup grace period.</summary>
    /// <example><code>var timeout = NdjsonDefaults.CleanupTimeout;</code></example>
    public static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The default observer notification deadline.</summary>
    /// <example><code>var timeout = NdjsonDefaults.ObserverTimeout;</code></example>
    public static readonly TimeSpan ObserverTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The default envelope-client input deadline.</summary>
    /// <example><code>var timeout = NdjsonDefaults.InputTimeout;</code></example>
    public static readonly TimeSpan InputTimeout = TimeSpan.FromSeconds(45);

}
