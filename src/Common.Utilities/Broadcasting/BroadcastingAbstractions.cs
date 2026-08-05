// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Publishes typed broadcasts to all active nodes in the requested or default scope.</summary>
/// <example><code>await service.PublishAsync(command, cancellationToken: token);</code></example>
public interface IBroadcastService
{
    /// <summary>Publishes one typed, short-lived broadcast.</summary>
    /// <remarks>
    /// A null, empty, or whitespace-only scope collection targets
    /// <see cref="BroadcastingOptions.DefaultScope"/>.
    /// </remarks>
    Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
        TBroadcast payload,
        IEnumerable<string> targetScopes = null,
        BroadcastPublishOptions options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Handles one accepted typed broadcast on a node.</summary>
/// <typeparam name="TBroadcast">The supported payload type.</typeparam>
/// <example><code>services.AddBroadcasting().AddHandler&lt;RefreshBroadcast, RefreshBroadcastHandler&gt;();</code></example>
public interface IBroadcastHandler<in TBroadcast>
{
    /// <summary>Handles an accepted broadcast asynchronously.</summary>
    Task HandleAsync(
        TBroadcast payload,
        BroadcastContext context,
        CancellationToken cancellationToken
    );
}

/// <summary>Stores live node-discovery registrations without storing messages.</summary>
/// <example><code>var nodes = await store.GetActiveAsync(["MyApp"], cancellationToken);</code></example>
public interface IBroadcastRegistryStore
{
    /// <summary>Gets provider capabilities used by the publishing lifecycle.</summary>
    BroadcastRegistryCapabilities Capabilities { get; }

    /// <summary>Upserts the complete registration and scope set for one node.</summary>
    Task UpsertAsync(
        BroadcastNodeRegistrationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Removes one node registration during graceful shutdown or maintenance.</summary>
    Task RemoveAsync(string nodeIdentity, CancellationToken cancellationToken = default);

    /// <summary>Returns one immutable active snapshot for the requested scopes.</summary>
    Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default
    );

    /// <summary>Returns a registration by node identity.</summary>
    Task<BroadcastNodeRegistration> FindAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    );

    /// <summary>Records a successful or failed direct delivery.</summary>
    Task RecordDeliveryAsync(
        string nodeIdentity,
        bool succeeded,
        string failure,
        CancellationToken cancellationToken = default
    );

    /// <summary>Renews the local registration lease.</summary>
    Task RenewLeaseAsync(
        string nodeIdentity,
        DateTimeOffset leaseExpiresUtc,
        CancellationToken cancellationToken = default
    );

    /// <summary>Marks expired leased registrations inactive.</summary>
    Task ExpireLeasesAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    /// <summary>Returns all registrations for privileged operational inspection.</summary>
    Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
        CancellationToken cancellationToken = default
    );
}

/// <summary>Sends an envelope directly to one remote node.</summary>
/// <example><code>var delivery = await transport.SendAsync(node, envelope, cancellationToken);</code></example>
public interface IBroadcastTransport
{
    /// <summary>Sends one direct delivery attempt.</summary>
    Task<BroadcastNodeDeliveryResult> SendAsync(
        BroadcastNodeRegistration target,
        BroadcastEnvelope envelope,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Validates and admits an inbound envelope for local execution.</summary>
/// <example><code>var delivery = await receiver.ReceiveAsync(envelope, cancellationToken);</code></example>
public interface IBroadcastReceiver
{
    /// <summary>Receives one local or remote envelope.</summary>
    Task<BroadcastNodeDeliveryResult> ReceiveAsync(
        BroadcastEnvelope envelope,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Admits already validated payloads to bounded node-local handler execution.</summary>
/// <example><code>dispatcher.TryDispatch(typeof(RefreshBroadcast), payload, context);</code></example>
public interface IBroadcastLocalDispatcher
{
    /// <summary>Attempts immediate admission to the registered type's bounded queue.</summary>
    bool TryDispatch(Type payloadType, object payload, BroadcastContext context);
}

/// <summary>Provides the identity used for the host's one registry registration.</summary>
/// <example><code>var nodeIdentity = identityProvider.GetNodeIdentity();</code></example>
public interface IBroadcastNodeIdentityProvider
{
    /// <summary>Returns the current process node identity.</summary>
    string GetNodeIdentity();
}

/// <summary>Optionally resolves a process-specific receiver address.</summary>
/// <example><code>var address = await resolver.ResolveAsync(cancellationToken);</code></example>
public interface IBroadcastNodeAddressResolver
{
    /// <summary>Returns a direct receiver address or <c>null</c> when unresolved.</summary>
    ValueTask<Uri> ResolveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Exposes safe, provider-neutral operational registry information.</summary>
/// <example><code>var snapshot = await diagnostics.GetAsync(cancellationToken);</code></example>
public interface IBroadcastingDiagnostics
{
    /// <summary>Returns registrations grouped by their display scope.</summary>
    Task<BroadcastingDiagnosticSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a stale registration when operational authorization permits it.</summary>
    Task<Result> RemoveAsync(string nodeIdentity, CancellationToken cancellationToken = default);
}

/// <summary>Authorizes privileged mutation of Broadcasting operational state.</summary>
/// <example><code>services.AddSingleton&lt;IBroadcastOperationalAuthorizer, MyAuthorizer&gt;();</code></example>
public interface IBroadcastOperationalAuthorizer
{
    /// <summary>Returns whether the caller may remove the requested node registration.</summary>
    ValueTask<bool> CanRemoveAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    );
}