// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Marks a typed command as belonging to Profiling's Broadcast integration.</summary>
/// <example><code>IRuntimeProfilingBroadcast command = new RuntimeProfilingStopBroadcast(sessionId, sessionKey);</code></example>
public interface IRuntimeProfilingBroadcast;

/// <summary>Contains Profiling's immutable target set for one control operation.</summary>
/// <remarks>
/// This is a Profiling coordination model. The standalone Broadcast service remains unaware of
/// fixed Profiling participants and continues to publish against its normal registry snapshot.
/// </remarks>
/// <example><code>var count = snapshot.TargetCount;</code></example>
public sealed record RuntimeProfilingBroadcastTargetSnapshot
{
    /// <summary>Creates a fixed target snapshot from active Broadcast registrations.</summary>
    /// <param name="targetScopes">The normalized target scopes.</param>
    /// <param name="targets">The active registrations selected for this operation.</param>
    /// <param name="senderNodeIdentity">The initiating Broadcast node identity.</param>
    /// <example><code>var snapshot = new RuntimeProfilingBroadcastTargetSnapshot(["MyApp"], registrations, "node-a");</code></example>
    public RuntimeProfilingBroadcastTargetSnapshot(
        IEnumerable<string> targetScopes,
        IEnumerable<BroadcastNodeRegistration> targets,
        string senderNodeIdentity
    )
    {
        this.TargetScopes = Array.AsReadOnly((targetScopes ?? []).ToArray());
        this.Targets = Array.AsReadOnly(
            (targets ?? [])
                .GroupBy(target => target.NodeIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    group.First() with
                    {
                        Scopes = Array.AsReadOnly(group.First().Scopes?.ToArray() ?? []),
                    }
                )
                .ToArray()
        );
        this.SenderNodeIdentity = senderNodeIdentity;
    }

    /// <summary>Gets the normalized Broadcast scopes.</summary>
    /// <example><code>var scopes = snapshot.TargetScopes;</code></example>
    public IReadOnlyCollection<string> TargetScopes { get; }

    /// <summary>Gets the fixed active registrations selected by Profiling.</summary>
    /// <example><code>var targets = snapshot.Targets;</code></example>
    public IReadOnlyList<BroadcastNodeRegistration> Targets { get; }

    /// <summary>Gets the initiating Broadcast node identity.</summary>
    /// <example><code>var sender = snapshot.SenderNodeIdentity;</code></example>
    public string SenderNodeIdentity { get; }

    /// <summary>Gets the target count.</summary>
    /// <example><code>var count = snapshot.TargetCount;</code></example>
    public int TargetCount => this.Targets.Count;
}

/// <summary>Contains the session values required by node-local profiling control handlers.</summary>
/// <param name="SessionId">The internal session identifier.</param>
/// <param name="SessionKey">The readable session key.</param>
/// <param name="Name">The session display name.</param>
/// <param name="StartedUtc">The logical UTC start.</param>
/// <param name="SamplingInterval">The scheduled sampling interval.</param>
/// <param name="Duration">The original collection duration.</param>
/// <param name="Tags">The copied plain session tags.</param>
/// <example><code>var session = RuntimeProfilingSessionBroadcast.From(currentSession);</code></example>
public sealed record RuntimeProfilingSessionBroadcast(
    Guid SessionId,
    string SessionKey,
    string Name,
    DateTimeOffset StartedUtc,
    TimeSpan SamplingInterval,
    TimeSpan Duration,
    IReadOnlyList<string> Tags
)
{
    /// <summary>Creates a payload model from the stored session.</summary>
    /// <param name="session">The session to copy.</param>
    /// <returns>The bounded session values required by a local handler.</returns>
    /// <example><code>var payload = RuntimeProfilingSessionBroadcast.From(session);</code></example>
    public static RuntimeProfilingSessionBroadcast From(RuntimeProfilingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new(
            session.Identity.Id,
            session.Identity.Key,
            session.Name,
            session.StartedUtc,
            session.SamplingInterval,
            session.Duration,
            session.Tags?.ToArray() ?? []
        );
    }

    /// <summary>Reconstructs the immutable session supplied to the node-local collector.</summary>
    /// <returns>The running profiling session.</returns>
    /// <example><code>var session = payload.ToSession();</code></example>
    public RuntimeProfilingSession ToSession() =>
        new()
        {
            Identity = new RuntimeProfilingSessionIdentity(this.SessionId, this.SessionKey),
            Name = this.Name,
            State = RuntimeProfilingSessionState.Running,
            StartedUtc = this.StartedUtc,
            EndsUtc = this.StartedUtc.Add(this.Duration),
            SamplingInterval = this.SamplingInterval,
            Duration = this.Duration,
            Tags = this.Tags?.ToArray() ?? [],
        };
}

/// <summary>Starts one node-local collector for a deployment-wide session.</summary>
/// <param name="Session">The bounded logical-session values.</param>
/// <example><code>var command = new RuntimeProfilingStartBroadcast(RuntimeProfilingSessionBroadcast.From(session));</code></example>
public sealed record RuntimeProfilingStartBroadcast(RuntimeProfilingSessionBroadcast Session)
    : IRuntimeProfilingBroadcast;

/// <summary>Stops one node-local collector without changing the original session end.</summary>
/// <param name="SessionId">The internal session identifier.</param>
/// <param name="SessionKey">The readable session key.</param>
/// <example><code>var command = new RuntimeProfilingStopBroadcast(session.Identity.Id, session.Identity.Key);</code></example>
public sealed record RuntimeProfilingStopBroadcast(Guid SessionId, string SessionKey)
    : IRuntimeProfilingBroadcast;

/// <summary>Collects one immediate node-local snapshot.</summary>
/// <param name="Session">The bounded logical-session values.</param>
/// <param name="Role">The default role used when the node has no existing participation.</param>
/// <example><code>var command = new RuntimeProfilingSnapshotBroadcast(session, RuntimeProfilingNodeRole.AdHocContributor);</code></example>
public sealed record RuntimeProfilingSnapshotBroadcast(
    RuntimeProfilingSessionBroadcast Session,
    RuntimeProfilingNodeRole Role
) : IRuntimeProfilingBroadcast;

/// <summary>Triggers one normal node-local garbage collection.</summary>
/// <param name="SessionId">The active session identifier, or an empty identifier while idle.</param>
/// <param name="SessionKey">The active readable session key, or <c>null</c> while idle.</param>
/// <example><code>var command = new RuntimeProfilingGarbageCollectionBroadcast(Guid.Empty, null);</code></example>
public sealed record RuntimeProfilingGarbageCollectionBroadcast(Guid SessionId, string SessionKey)
    : IRuntimeProfilingBroadcast;
