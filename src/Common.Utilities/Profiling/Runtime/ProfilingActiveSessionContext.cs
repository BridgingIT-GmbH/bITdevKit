// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Exposes the process-local profiling session and node currently accepting metric observations.
/// </summary>
/// <remarks>
/// This context is updated by node-local collection and programmatic measurement. It is not a
/// session-store replacement and does not coordinate deployment-wide lifecycle state.
/// </remarks>
/// <example><code>var current = context.Current;</code></example>
public sealed class ProfilingActiveSessionContext
{
    private ProfilingActiveSession current;

    /// <summary>Gets the current process-local session and node, when collection is active.</summary>
    /// <example><code>if (context.Current is { } current) { /* associate an observation */ }</code></example>
    public ProfilingActiveSession Current => Volatile.Read(ref this.current);

    /// <summary>Associates the process with a running session and its stable node.</summary>
    /// <param name="session">The running logical session.</param>
    /// <param name="node">The process-local profiling node.</param>
    /// <example><code>context.Set(session, node);</code></example>
    public void Set(ProfilingSession session, ProfilingNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);
        Volatile.Write(ref this.current, new ProfilingActiveSession(session, node));
    }

    /// <summary>Clears the context only when it still refers to the supplied session.</summary>
    /// <param name="sessionId">The internal session identifier owned by runtime code.</param>
    /// <example><code>context.Clear(session.Identity.Id);</code></example>
    public void Clear(Guid sessionId)
    {
        while (true)
        {
            var current = Volatile.Read(ref this.current);
            if (current is null || current.Session.Identity.Id != sessionId)
            {
                return;
            }

            if (
                ReferenceEquals(
                    Interlocked.CompareExchange(ref this.current, null, current),
                    current
                )
            )
            {
                return;
            }
        }
    }
}

/// <summary>Contains the process-local session and stable node used for metric association.</summary>
/// <param name="Session">The logical session.</param>
/// <param name="Node">The process-local profiling node.</param>
/// <example><code>var sessionKey = current.Session.Identity.Key;</code></example>
public sealed record ProfilingActiveSession(ProfilingSession Session, ProfilingNode Node);
