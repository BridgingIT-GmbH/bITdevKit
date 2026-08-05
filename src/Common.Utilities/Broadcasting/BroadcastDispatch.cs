// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Handles the built-in delivery probe without performing application work.</summary>
/// <example><code>services.AddBroadcasting(options => options.Scopes("MyApp"));</code></example>
public sealed class BroadcastProbeHandler : IBroadcastHandler<BroadcastProbe>
{
    /// <inheritdoc />
    public Task HandleAsync(
        BroadcastProbe payload,
        BroadcastContext context,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

/// <summary>Represents one validated payload accepted for node-local dispatch.</summary>
/// <param name="Registration">The registered payload and handler mapping.</param>
/// <param name="Payload">The deserialized payload.</param>
/// <param name="Context">The broadcast metadata supplied to the handler.</param>
/// <example><code>var accepted = new AcceptedBroadcast(registration, payload, context);</code></example>
public sealed record AcceptedBroadcast(
    BroadcastHandlerRegistration Registration,
    object Payload,
    BroadcastContext Context
);

/// <summary>Provides bounded, expiry-aware duplicate protection for recently accepted broadcasts.</summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <example><code>if (tracker.TryReserve(id, now)) { tracker.Commit(id, now); }</code></example>
public sealed class RecentBroadcastTracker(BroadcastingOptions options)
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, DateTimeOffset> entries = [];
    private readonly HashSet<Guid> reservations = [];

    /// <summary>Atomically reserves a broadcast identifier when it is not already tracked.</summary>
    public bool TryReserve(Guid id, DateTimeOffset now)
    {
        lock (this.sync)
        {
            this.Evict(now);
            if (this.entries.ContainsKey(id) || !this.reservations.Add(id))
            {
                return false;
            }

            if (this.entries.Count + this.reservations.Count <= options.DuplicateCapacity)
            {
                return true;
            }

            foreach (
                var entryId in this
                    .entries.OrderBy(x => x.Value)
                    .Take(this.entries.Count + this.reservations.Count - options.DuplicateCapacity)
                    .Select(x => x.Key)
                    .ToArray()
            )
            {
                this.entries.Remove(entryId);
            }

            if (this.entries.Count + this.reservations.Count <= options.DuplicateCapacity)
            {
                return true;
            }

            this.reservations.Remove(id);
            return false;
        }
    }

    /// <summary>Commits a reservation as an accepted identifier until retention expires.</summary>
    public void Commit(Guid id, DateTimeOffset now)
    {
        lock (this.sync)
        {
            this.reservations.Remove(id);
            this.entries[id] = now + options.DuplicateRetention;
            this.Evict(now);
        }
    }

    /// <summary>Releases an uncommitted reservation so a later delivery may retry.</summary>
    public void Release(Guid id)
    {
        lock (this.sync)
        {
            this.reservations.Remove(id);
        }
    }

    private void Evict(DateTimeOffset now)
    {
        foreach (var id in this.entries.Where(x => x.Value <= now).Select(x => x.Key).ToArray())
        {
            this.entries.Remove(id);
        }

        if (this.entries.Count <= options.DuplicateCapacity)
        {
            return;
        }

        foreach (
            var id in this
                .entries.OrderBy(x => x.Value)
                .Take(this.entries.Count - options.DuplicateCapacity)
                .Select(x => x.Key)
                .ToArray()
        )
        {
            this.entries.Remove(id);
        }
    }
}

/// <summary>Admits validated broadcasts to one bounded, ordered handler queue per payload type.</summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <param name="registrationState">The shared handler registration state.</param>
/// <param name="scopeFactory">The scope factory used to resolve handlers per execution.</param>
/// <param name="logger">The optional structured logger.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <example><code>dispatcher.TryDispatch(typeof(RefreshBroadcast), payload, context);</code></example>
public sealed class BroadcastLocalDispatcher(
    BroadcastingOptions options,
    BroadcastingRegistrationState registrationState,
    IServiceScopeFactory scopeFactory,
    ILogger<BroadcastLocalDispatcher> logger = null,
    IMetricsService metrics = null
) : IBroadcastLocalDispatcher
{
    private readonly ConcurrentDictionary<Type, Channel<AcceptedBroadcast>> channels = new(
        (options.Enabled ? registrationState.Handlers : []).ToDictionary(
            x => x.PayloadType,
            _ =>
                Channel.CreateBounded<AcceptedBroadcast>(
                    new BoundedChannelOptions(options.HandlerQueueCapacity)
                    {
                        SingleReader = true,
                        SingleWriter = false,
                        FullMode = BoundedChannelFullMode.Wait,
                    }
                )
        )
    );
    private readonly List<Task> readers = [];
    private CancellationTokenSource stopping;

    /// <summary>Attempts to admit an accepted broadcast to its registered handler queue.</summary>
    public bool TryDispatch(AcceptedBroadcast accepted)
    {
        return this.channels.TryGetValue(accepted.Registration.PayloadType, out var channel)
            && channel.Writer.TryWrite(accepted);
    }

    /// <inheritdoc />
    public bool TryDispatch(Type payloadType, object payload, BroadcastContext context)
    {
        var registration = registrationState.Handlers.SingleOrDefault(x =>
            x.PayloadType == payloadType
        );
        return registration is not null && this.TryDispatch(new(registration, payload, context));
    }

    /// <summary>Starts one queue reader for each registered payload type.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled || this.stopping is not null)
        {
            return Task.CompletedTask;
        }

        this.stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        foreach (var channel in this.channels.Values)
        {
            this.readers.Add(
                Task.Run(
                    () => this.ReadAsync(channel.Reader, this.stopping.Token),
                    CancellationToken.None
                )
            );
        }

        return Task.CompletedTask;
    }

    /// <summary>Completes all queues and stops their readers.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this.stopping is null)
        {
            return;
        }

        foreach (var channel in this.channels.Values)
        {
            channel.Writer.TryComplete();
        }

        this.stopping.Cancel();
        try
        {
            await Task.WhenAll(this.readers).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown cancellation is expected.
        }
        finally
        {
            this.stopping.Dispose();
            this.stopping = null;
        }
    }

    private async Task ReadAsync(
        ChannelReader<AcceptedBroadcast> reader,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await foreach (
                var accepted in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                using var correlationScope = CorrelationId.BeginScope(
                    accepted.Context.CorrelationId
                );
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await accepted
                        .Registration.InvokeAsync(
                            scope.ServiceProvider,
                            accepted.Payload,
                            accepted.Context,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    metrics?.Increment(
                        "broadcasting_handler_failure",
                        Metrics.NormalizePart(accepted.Registration.TypeName)
                    );
                    if (logger is not null)
                    {
                        BroadcastingTypedLogger.LogHandlerFailed(
                            logger,
                            "UTL",
                            accepted.Registration.TypeName,
                            exception
                        );
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}

/// <summary>Connects the node-local dispatcher lifecycle to ASP.NET Core hosting.</summary>
/// <param name="dispatcher">The shared node-local dispatcher.</param>
/// <example><code>services.AddSingleton&lt;IHostedService, BroadcastLocalDispatchHostedService&gt;();</code></example>
public sealed class BroadcastLocalDispatchHostedService(BroadcastLocalDispatcher dispatcher)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        dispatcher.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        dispatcher.StopAsync(cancellationToken);
}

/// <summary>Validates inbound envelopes and admits supported payloads to local execution.</summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <param name="registrationState">The shared handler registration state.</param>
/// <param name="serializer">The payload serializer.</param>
/// <param name="recentBroadcasts">The duplicate-protection tracker.</param>
/// <param name="dispatcher">The node-local dispatcher.</param>
/// <param name="timeProvider">The provider-neutral clock.</param>
/// <param name="identityProvider">The local node identity provider.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <param name="logger">The optional structured logger.</param>
/// <example><code>var result = await receiver.ReceiveAsync(envelope, cancellationToken);</code></example>
public sealed class BroadcastReceiver(
    BroadcastingOptions options,
    BroadcastingRegistrationState registrationState,
    ISerializer serializer,
    RecentBroadcastTracker recentBroadcasts,
    BroadcastLocalDispatcher dispatcher,
    TimeProvider timeProvider,
    IBroadcastNodeIdentityProvider identityProvider,
    IMetricsService metrics = null,
    ILogger<BroadcastReceiver> logger = null
) : IBroadcastReceiver
{
    /// <inheritdoc />
    public Task<BroadcastNodeDeliveryResult> ReceiveAsync(
        BroadcastEnvelope envelope,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localIdentity = identityProvider.GetNodeIdentity();

        if (
            envelope is null
            || envelope.ProtocolVersion != 1
            || envelope.BroadcastId == Guid.Empty
            || envelope.TargetScopes is null
            || envelope.TargetScopes.Count == 0
        )
        {
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.Rejected,
                envelope?.Type,
                "Invalid envelope."
            );
        }

        var localScopes = new HashSet<string>(options.Scopes, StringComparer.OrdinalIgnoreCase);
        if (!envelope.TargetScopes.Any(localScopes.Contains))
        {
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.Rejected,
                envelope.Type,
                "Target scope is not configured locally."
            );
        }

        var now = timeProvider.GetUtcNow();
        if (
            envelope.CreatedUtc == default
            || envelope.ExpiresUtc <= envelope.CreatedUtc
            || envelope.ExpiresUtc - envelope.CreatedUtc >= options.DuplicateRetention
        )
        {
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.Rejected,
                envelope.Type,
                "Envelope timestamps are invalid."
            );
        }

        if (envelope.ExpiresUtc <= now)
        {
            return this.Complete(localIdentity, BroadcastDeliveryOutcome.Expired, envelope.Type);
        }

        if (envelope.Payload is null || envelope.Payload.LongLength > options.MaximumPayloadBytes)
        {
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.Rejected,
                envelope.Type,
                "Payload exceeds the configured limit."
            );
        }

        var registration = registrationState.Handlers.SingleOrDefault(x =>
            string.Equals(x.TypeName, envelope.Type, StringComparison.Ordinal)
        );
        if (registration is null)
        {
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.Unsupported,
                envelope.Type
            );
        }

        if (!recentBroadcasts.TryReserve(envelope.BroadcastId, now))
        {
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.AlreadyProcessed,
                envelope.Type
            );
        }

        try
        {
            using var stream = new MemoryStream(envelope.Payload, writable: false);
            var payload = serializer.Deserialize(stream, registration.PayloadType);
            if (payload is null)
            {
                recentBroadcasts.Release(envelope.BroadcastId);
                return this.Complete(
                    localIdentity,
                    BroadcastDeliveryOutcome.Rejected,
                    envelope.Type,
                    "Payload could not be deserialized."
                );
            }

            var context = new BroadcastContext(
                envelope.BroadcastId,
                envelope.TargetScopes,
                envelope.CreatedUtc,
                envelope.ExpiresUtc,
                envelope.CorrelationId,
                envelope.SenderNodeIdentity
            );
            if (!dispatcher.TryDispatch(new(registration, payload, context)))
            {
                recentBroadcasts.Release(envelope.BroadcastId);
                return this.Complete(
                    localIdentity,
                    BroadcastDeliveryOutcome.Rejected,
                    envelope.Type,
                    "Local handler queue is full."
                );
            }

            recentBroadcasts.Commit(envelope.BroadcastId, now);
            return this.Complete(localIdentity, BroadcastDeliveryOutcome.Accepted, envelope.Type);
        }
        catch (OperationCanceledException)
        {
            recentBroadcasts.Release(envelope.BroadcastId);
            throw;
        }
        catch (Exception)
        {
            recentBroadcasts.Release(envelope.BroadcastId);
            return this.Complete(
                localIdentity,
                BroadcastDeliveryOutcome.Rejected,
                envelope.Type,
                "Payload is malformed."
            );
        }
    }

    private Task<BroadcastNodeDeliveryResult> Complete(
        string nodeIdentity,
        BroadcastDeliveryOutcome outcome,
        string broadcastType,
        string detail = null
    )
    {
        BroadcastingMetrics.RecordReceiver(metrics, broadcastType, outcome);
        if (logger is not null)
        {
            BroadcastingTypedLogger.LogReceiverOutcome(
                logger,
                "UTL",
                broadcastType ?? "unknown",
                outcome.ToString()
            );
        }

        return Task.FromResult(new BroadcastNodeDeliveryResult(nodeIdentity, outcome, detail));
    }
}
