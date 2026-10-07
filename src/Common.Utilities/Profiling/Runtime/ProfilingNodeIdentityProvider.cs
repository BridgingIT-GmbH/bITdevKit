// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Reflection;

/// <summary>Creates and caches one process descriptor without persistence or Broadcast dependencies.</summary>
/// <example><code>var identity = new ProfilingNodeIdentityProvider().GetNode();</code></example>
public sealed class ProfilingNodeIdentityProvider : IProfilingNodeIdentityProvider
{
    private readonly ProfilingNode node;

    /// <summary>Creates the process-lifetime cache with optional host display metadata.</summary>
    /// <example><code>var identity = new ProfilingNodeIdentityProvider(displayName: "worker-a");</code></example>
    public ProfilingNodeIdentityProvider(TimeProvider timeProvider = null, string displayName = null, string applicationVersion = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        DateTimeOffset processStartedUtc;
        try
        {
            using var process = Process.GetCurrentProcess();
            processStartedUtc = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (InvalidOperationException)
        {
            processStartedUtc = clock.GetUtcNow().ToUniversalTime();
        }

        this.node = new ProfilingNode
        {
            Identity = ProfilingIdentityFactory.CreateNode(),
            HostName = Bound(Environment.MachineName),
            DisplayName = Bound(displayName ?? Environment.MachineName),
            ProcessId = Environment.ProcessId,
            ProcessStartedUtc = processStartedUtc,
            ApplicationVersion = Bound(applicationVersion ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString())
        };
    }

    /// <inheritdoc />
    public ProfilingNode GetNode() => this.node;

    private static string Bound(string value)
    {
        if (value is null || value.Length <= 128)
        {
            return value;
        }

        return value[..(char.IsHighSurrogate(value[127]) ? 127 : 128)];
    }
}
