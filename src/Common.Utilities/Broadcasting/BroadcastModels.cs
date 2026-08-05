// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Describes the immediate receiver outcome for one node.</summary>
/// <example><code>var accepted = outcome == BroadcastDeliveryOutcome.Accepted;</code></example>
public enum BroadcastDeliveryOutcome
{
    /// <summary>The node admitted the broadcast for local execution.</summary>
    Accepted,

    /// <summary>The node already accepted this broadcast identifier.</summary>
    AlreadyProcessed,

    /// <summary>The broadcast expired before acceptance.</summary>
    Expired,

    /// <summary>The node has no registered handler for the type.</summary>
    Unsupported,

    /// <summary>The node rejected the request or local queue admission.</summary>
    Rejected,

    /// <summary>The node returned an unexpected processing failure.</summary>
    Failed,

    /// <summary>The node could not be contacted.</summary>
    Unreachable,

    /// <summary>The node did not respond before the delivery deadline.</summary>
    TimedOut,
}

/// <summary>Contains transport-neutral metadata and one serialized payload.</summary>
/// <param name="BroadcastId">The globally unique publication identifier.</param>
/// <param name="Type">The stable registered payload type name.</param>
/// <param name="TargetScopes">The scopes selected for delivery.</param>
/// <param name="Payload">The serialized payload bytes.</param>
/// <param name="CreatedUtc">The UTC publication timestamp.</param>
/// <param name="ExpiresUtc">The UTC expiration timestamp.</param>
/// <param name="CorrelationId">The optional application correlation identifier, independent from the distributed trace identifier.</param>
/// <param name="ProtocolVersion">The transport protocol version.</param>
/// <param name="SenderNodeIdentity">The publishing node identity when available.</param>
/// <example><code>var typeName = envelope.Type;</code></example>
public sealed record BroadcastEnvelope(
    Guid BroadcastId,
    string Type,
    IReadOnlyCollection<string> TargetScopes,
    byte[] Payload,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc,
    string CorrelationId = null,
    int ProtocolVersion = 1,
    string SenderNodeIdentity = null
);

/// <summary>Configures one publication without changing host defaults.</summary>
/// <example><code>var options = new BroadcastPublishOptions { RequireAtLeastOneTarget = true };</code></example>
public sealed record BroadcastPublishOptions
{
    /// <summary>Gets an optional lifetime override.</summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>Gets whether no active targets should fail the publication.</summary>
    public bool RequireAtLeastOneTarget { get; init; }
}

/// <summary>Supplies metadata to a node-local typed handler.</summary>
/// <param name="BroadcastId">The publication identifier.</param>
/// <param name="TargetScopes">The scopes selected for delivery.</param>
/// <param name="CreatedUtc">The UTC publication timestamp.</param>
/// <param name="ExpiresUtc">The UTC expiration timestamp.</param>
/// <param name="CorrelationId">The optional application correlation identifier, independent from the distributed trace identifier.</param>
/// <param name="SenderNodeIdentity">The publishing node identity when available.</param>
/// <example><code>logger.LogDebug("Handling {BroadcastId}", context.BroadcastId);</code></example>
public sealed record BroadcastContext(
    Guid BroadcastId,
    IReadOnlyCollection<string> TargetScopes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc,
    string CorrelationId,
    string SenderNodeIdentity = null
);

/// <summary>Represents the built-in no-op payload used to verify broadcast delivery.</summary>
/// <param name="ProbeId">The unique probe identifier.</param>
/// <param name="RequestedUtc">The UTC timestamp at which the probe was requested.</param>
/// <example><code>await service.PublishAsync(new BroadcastProbe(Guid.NewGuid(), DateTimeOffset.UtcNow));</code></example>
public sealed record BroadcastProbe(Guid ProbeId, DateTimeOffset RequestedUtc);

/// <summary>Describes registry-provider runtime capabilities.</summary>
/// <param name="IsShared">Whether registrations are shared between processes.</param>
/// <param name="RequiresAdvertisedAddress">Whether nodes must advertise a reachable address.</param>
/// <example><code>if (store.Capabilities.IsShared) { /* remote delivery is available */ }</code></example>
public sealed record BroadcastRegistryCapabilities(bool IsShared, bool RequiresAdvertisedAddress);

/// <summary>Describes one active or inactive node registration.</summary>
/// <example><code>var identity = registration.NodeIdentity;</code></example>
public sealed record BroadcastNodeRegistration
{
    /// <summary>Gets the stable process identity.</summary>
    public string NodeIdentity { get; init; }

    /// <summary>Gets the directly reachable receiver address.</summary>
    public Uri AdvertisedAddress { get; init; }

    /// <summary>Gets the scopes subscribed by the process.</summary>
    public IReadOnlyCollection<string> Scopes { get; init; } = [];

    /// <summary>Gets when the process started.</summary>
    public DateTimeOffset ProcessStartedUtc { get; init; }

    /// <summary>Gets when this registration was last written.</summary>
    public DateTimeOffset RegisteredUtc { get; init; }

    /// <summary>Gets the protocol version advertised by the node.</summary>
    public string ProtocolVersion { get; init; } = "1";

    /// <summary>Gets whether the node participates in target snapshots.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Gets the latest successful delivery timestamp.</summary>
    public DateTimeOffset? LastSuccessUtc { get; init; }

    /// <summary>Gets the latest failed delivery timestamp.</summary>
    public DateTimeOffset? LastFailureUtc { get; init; }

    /// <summary>Gets the latest safe failure summary.</summary>
    public string LastFailure { get; init; }

    /// <summary>Gets the consecutive failed-delivery count.</summary>
    public int ConsecutiveFailureCount { get; init; }

    /// <summary>Gets the optional lease-expiration timestamp.</summary>
    public DateTimeOffset? LeaseExpiresUtc { get; init; }

    /// <summary>Gets when the optional registration lease was last renewed.</summary>
    public DateTimeOffset? LeaseRenewedUtc { get; init; }
}

/// <summary>Provides the values used to upsert one process registration.</summary>
/// <param name="NodeIdentity">The stable process identity.</param>
/// <param name="AdvertisedAddress">The directly reachable receiver address.</param>
/// <param name="Scopes">The scopes subscribed by the process.</param>
/// <param name="ProcessStartedUtc">When the process started.</param>
/// <param name="RegisteredUtc">When the registration is written.</param>
/// <param name="LeaseExpiresUtc">The optional registration lease expiration.</param>
/// <param name="ProtocolVersion">The protocol version advertised by the node.</param>
/// <example><code>await store.UpsertAsync(request, cancellationToken);</code></example>
public sealed record BroadcastNodeRegistrationRequest(
    string NodeIdentity,
    Uri AdvertisedAddress,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset ProcessStartedUtc,
    DateTimeOffset RegisteredUtc,
    DateTimeOffset? LeaseExpiresUtc,
    string ProtocolVersion = "1"
);

/// <summary>Describes the immediate delivery outcome for one target node.</summary>
/// <param name="NodeIdentity">The target node identity.</param>
/// <param name="Outcome">The immediate receiver or transport outcome.</param>
/// <param name="Detail">An optional safe outcome description.</param>
/// <param name="Duration">The optional delivery duration.</param>
/// <example><code>if (result.Outcome == BroadcastDeliveryOutcome.Accepted) { /* accepted */ }</code></example>
public sealed record BroadcastNodeDeliveryResult(
    string NodeIdentity,
    BroadcastDeliveryOutcome Outcome,
    string Detail = null,
    TimeSpan? Duration = null
);

/// <summary>Contains aggregate and per-node outcomes for one publication.</summary>
/// <example><code>logger.LogInformation("Accepted by {Count} nodes", result.AcceptedCount);</code></example>
public sealed record BroadcastResult
{
    /// <summary>Gets the published broadcast identifier.</summary>
    public Guid BroadcastId { get; init; }

    /// <summary>Gets the normalized display scopes targeted by the publication.</summary>
    public IReadOnlyCollection<string> TargetScopes { get; init; } = [];

    /// <summary>Gets when the delivery operation started.</summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets when the delivery operation completed.</summary>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>Gets the deterministic per-node outcomes.</summary>
    public IReadOnlyList<BroadcastNodeDeliveryResult> Nodes { get; init; } = [];

    /// <summary>Gets the selected target count.</summary>
    public int TargetCount => this.Nodes.Count;

    /// <summary>Gets the number of nodes that returned a receiver outcome.</summary>
    public int ResponseCount =>
        this.Nodes.Count(x =>
            x.Outcome
                is not BroadcastDeliveryOutcome.Unreachable
                    and not BroadcastDeliveryOutcome.TimedOut
        );

    /// <summary>Gets the number of nodes that accepted the broadcast.</summary>
    public int AcceptedCount =>
        this.Nodes.Count(x => x.Outcome == BroadcastDeliveryOutcome.Accepted);

    /// <summary>Gets the number of unsupported, rejected, failed, unreachable, or timed-out deliveries.</summary>
    public int FailureCount =>
        this.Nodes.Count(x =>
            x.Outcome
                is BroadcastDeliveryOutcome.Unsupported
                    or BroadcastDeliveryOutcome.Rejected
                    or BroadcastDeliveryOutcome.Failed
                    or BroadcastDeliveryOutcome.Unreachable
                    or BroadcastDeliveryOutcome.TimedOut
        );

    /// <summary>Gets the number of nodes that could not be contacted.</summary>
    public int UnreachableCount =>
        this.Nodes.Count(x => x.Outcome == BroadcastDeliveryOutcome.Unreachable);

    /// <summary>Gets the number of nodes that reported an expired broadcast.</summary>
    public int ExpiredCount => this.Nodes.Count(x => x.Outcome == BroadcastDeliveryOutcome.Expired);
}

/// <summary>Contains a provider-neutral operational view of Broadcasting.</summary>
/// <param name="Enabled">Whether the Broadcasting runtime is enabled.</param>
/// <param name="Scopes">The configured scopes and their registered nodes.</param>
/// <example><code>var snapshot = await diagnostics.GetAsync(cancellationToken);</code></example>
public sealed record BroadcastingDiagnosticSnapshot(
    bool Enabled,
    IReadOnlyList<BroadcastScopeDiagnostic> Scopes
);

/// <summary>Groups registered nodes under one configured scope.</summary>
/// <param name="Scope">The configured scope name.</param>
/// <param name="Nodes">The nodes registered for the scope.</param>
/// <example><code>var nodeCount = scope.Nodes.Count;</code></example>
public sealed record BroadcastScopeDiagnostic(
    string Scope,
    IReadOnlyList<BroadcastNodeRegistration> Nodes
);