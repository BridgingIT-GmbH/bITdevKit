// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using System.Collections.Immutable;

/// <summary>Tracks actual handler invocations within one awaited logical dispatch, including concurrent branches.</summary>
/// <example><code>using var invocation = new AuditRequestInvocation(request);</code></example>
public sealed class AuditRequestInvocation : IDisposable
{
    private static readonly AsyncLocal<ImmutableStack<AuditRequestInvocation>> ambient = new();
    private readonly ImmutableStack<AuditRequestInvocation> previous;
    private readonly object request;
    private readonly ConcurrentDictionary<Type, int> attempts = new();
    private int count;
    private string executionStatus;
    private int disposed;

    /// <summary>Owns a flow-local dispatch frame without modifying its parent or sibling frames.</summary>
    /// <example><code>using var invocation = new AuditRequestInvocation(command);</code></example>
    public AuditRequestInvocation(object request)
    {
        this.request = request;
        this.previous = ambient.Value ?? ImmutableStack<AuditRequestInvocation>.Empty;
        ambient.Value = this.previous.Push(this);
    }

    /// <summary>The count of actual invoked handlers, independent of capture truncation.</summary>
    /// <example><code>var shortCircuited = invocation.Count == 0;</code></example>
    public int Count => Volatile.Read(ref this.count);

    /// <summary>An explicitly observed source policy status, or null when no source supplied one.</summary>
    /// <example><code>var status = invocation.ExecutionStatus;</code></example>
    public string ExecutionStatus => Volatile.Read(ref this.executionStatus);

    /// <summary>Reports a known source status without creating capture or inferring a policy outcome.</summary>
    /// <example><code>AuditRequestInvocation.Observe(request, "CacheHit");</code></example>
    public static void Observe(object request, string status)
    {
        var frame = (ambient.Value ?? ImmutableStack<AuditRequestInvocation>.Empty).FirstOrDefault(item => ReferenceEquals(item.request, request) && Volatile.Read(ref item.disposed) == 0);
        if (frame is not null) Volatile.Write(ref frame.executionStatus, AuditStructuralValidation.Required(status, 128, nameof(status)));
    }

    /// <summary>Records an actual invocation on its matching dispatch frame, returning a per-handler attempt when bounded.</summary>
    /// <example><code>var attempt = AuditRequestInvocation.Invoked(command, typeof(RenameCustomerHandler));</code></example>
    public static int? Invoked(object request, Type handler)
    {
        var frame = (ambient.Value ?? ImmutableStack<AuditRequestInvocation>.Empty).FirstOrDefault(item => ReferenceEquals(item.request, request) && Volatile.Read(ref item.disposed) == 0);
        if (frame is null || handler is null) return null;
        Interlocked.Increment(ref frame.count);
        if (frame.attempts.Count >= 1_024 && !frame.attempts.ContainsKey(handler)) return null;
        return frame.attempts.AddOrUpdate(handler, 1, (_, value) => value == int.MaxValue ? value : value + 1);
    }

    /// <summary>Releases this flow's dispatch metadata when its awaited owner ends.</summary>
    /// <example><code>invocation.Dispose();</code></example>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0) ambient.Value = this.previous;
    }
}
