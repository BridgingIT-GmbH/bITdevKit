// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides the process-local fallback transport used when no remote transport is configured.
/// </summary>
/// <example><code>services.AddSingleton&lt;IBroadcastTransport, LocalOnlyBroadcastTransport&gt;();</code></example>
public sealed class LocalOnlyBroadcastTransport : IBroadcastTransport
{
    /// <inheritdoc />
    public Task<BroadcastNodeDeliveryResult> SendAsync(
        BroadcastNodeRegistration target,
        BroadcastEnvelope envelope,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            new BroadcastNodeDeliveryResult(
                target.NodeIdentity,
                BroadcastDeliveryOutcome.Unreachable,
                "No remote broadcast transport is configured."
            )
        );
}

/// <summary>
/// Publishes typed broadcast payloads to a fixed snapshot of active local and remote nodes.
/// </summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <param name="identityProvider">The local node identity provider.</param>
/// <param name="registry">The effective node registry.</param>
/// <param name="receiver">The local receiver used for self-delivery.</param>
/// <param name="transport">The effective remote transport.</param>
/// <param name="serializer">The payload serializer.</param>
/// <param name="timeProvider">The provider-neutral clock.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <param name="logger">The optional structured logger.</param>
/// <example><code>var result = await service.PublishAsync(payload, cancellationToken: token);</code></example>
public sealed class BroadcastService(
    BroadcastingOptions options,
    IBroadcastNodeIdentityProvider identityProvider,
    IBroadcastRegistryStore registry,
    IBroadcastReceiver receiver,
    IBroadcastTransport transport,
    ISerializer serializer,
    TimeProvider timeProvider,
    IMetricsService metrics = null,
    ILogger<BroadcastService> logger = null
) : IBroadcastService
{
    /// <inheritdoc />
    public async Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
        TBroadcast payload,
        IEnumerable<string> targetScopes = null,
        BroadcastPublishOptions publishOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled)
        {
            return Result<BroadcastResult>.Failure().WithError(new BroadcastingDisabledError());
        }

        var scopes = (targetScopes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (scopes.Length == 0)
        {
            scopes = [BroadcastingOptions.DefaultScope];
        }

        if (payload is null)
        {
            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastValidationError("A broadcast payload is required.")
                );
        }

        options.Validate();
        var lifetime = publishOptions?.Lifetime ?? options.DefaultLifetime;
        if (lifetime <= TimeSpan.Zero || lifetime >= options.DuplicateRetention)
        {
            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastValidationError(
                        "The broadcast lifetime must be positive and shorter than duplicate retention."
                    )
                );
        }

        var configuredScopes = new HashSet<string>(
            options.Scopes,
            StringComparer.OrdinalIgnoreCase
        );
        var forbiddenScope = scopes.FirstOrDefault(x => !configuredScopes.Contains(x));
        if (forbiddenScope is not null)
        {
            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastScopeForbiddenError(
                        $"The target scope '{forbiddenScope}' is not configured for this host."
                    )
                );
        }

        byte[] serialized;
        try
        {
            using var stream = new MemoryStream();
            serializer.Serialize(payload, stream);
            serialized = stream.ToArray();
        }
        catch (Exception)
        {
            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastSerializationError(
                        $"Broadcast serialization failed for type '{typeof(TBroadcast).FullName}'."
                    )
                );
        }

        if (serialized.LongLength > options.MaximumPayloadBytes)
        {
            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastValidationError("The serialized broadcast payload is too large.")
                );
        }

        var typeName = typeof(TBroadcast).FullName;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastValidationError("The broadcast type has no stable full name.")
                );
        }

        IReadOnlyList<BroadcastNodeRegistration> targets;
        var identity = identityProvider.GetNodeIdentity();
        try
        {
            if (registry.Capabilities.IsShared)
            {
                var sender = await registry
                    .FindAsync(identity, cancellationToken)
                    .ConfigureAwait(false);
                if (sender is null || !sender.IsActive)
                {
                    return Result<BroadcastResult>
                        .Failure()
                        .WithError(
                            new BroadcastSenderNotRegisteredError(
                                "The publishing node is not active in the shared broadcast registry."
                            )
                        );
                }

                var senderScopes = new HashSet<string>(
                    sender.Scopes,
                    StringComparer.OrdinalIgnoreCase
                );
                forbiddenScope = scopes.FirstOrDefault(scope => !senderScopes.Contains(scope));
                if (forbiddenScope is not null)
                {
                    return Result<BroadcastResult>
                        .Failure()
                        .WithError(
                            new BroadcastScopeForbiddenError(
                                $"The target scope '{forbiddenScope}' is not present in the sender registration."
                            )
                        );
                }
            }

            targets = (
                await registry.GetActiveAsync(scopes, cancellationToken).ConfigureAwait(false)
            )
                .GroupBy(target => target.NodeIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (logger is not null)
            {
                BroadcastingTypedLogger.LogRegistryFailure(logger, "UTL", "snapshot", exception);
            }

            return Result<BroadcastResult>
                .Failure()
                .WithError(
                    new BroadcastRegistryUnavailableError("The broadcast registry is unavailable.")
                );
        }

        if (targets.Count == 0 && publishOptions?.RequireAtLeastOneTarget == true)
        {
            return Result<BroadcastResult>.Failure().WithError(new BroadcastNoTargetError());
        }

        var now = timeProvider.GetUtcNow();
        var correlationId = CorrelationId.Current;
        if (!CorrelationId.IsValid(correlationId))
        {
            correlationId = null;
        }

        var envelope = new BroadcastEnvelope(
            Guid.NewGuid(),
            typeName,
            scopes,
            serialized,
            now,
            now + lifetime,
            correlationId,
            SenderNodeIdentity: identity
        );
        if (logger is not null)
        {
            BroadcastingTypedLogger.LogPublicationStarted(logger, "UTL", typeName, scopes.Length);
        }

        var results = new ConcurrentDictionary<string, BroadcastNodeDeliveryResult>(
            StringComparer.OrdinalIgnoreCase
        );

        await Parallel
            .ForEachAsync(
                targets,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = options.MaximumConcurrentDeliveries,
                },
                async (target, token) =>
                {
                    var started = timeProvider.GetTimestamp();
                    BroadcastNodeDeliveryResult result;
                    try
                    {
                        if (
                            string.Equals(
                                target.NodeIdentity,
                                identity,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            result = await receiver
                                .ReceiveAsync(envelope, token)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            var remaining = envelope.ExpiresUtc - timeProvider.GetUtcNow();
                            if (remaining <= TimeSpan.Zero)
                            {
                                result = new(target.NodeIdentity, BroadcastDeliveryOutcome.Expired);
                            }
                            else
                            {
                                using var deadline =
                                    CancellationTokenSource.CreateLinkedTokenSource(token);
                                deadline.CancelAfter(
                                    remaining < options.DeliveryTimeout
                                        ? remaining
                                        : options.DeliveryTimeout
                                );
                                result = await transport
                                    .SendAsync(target, envelope, deadline.Token)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        result = new(target.NodeIdentity, BroadcastDeliveryOutcome.TimedOut);
                    }
                    catch (Exception)
                    {
                        result = new(target.NodeIdentity, BroadcastDeliveryOutcome.Unreachable);
                    }

                    result = result with { NodeIdentity = target.NodeIdentity };
                    result = result with { Duration = timeProvider.GetElapsedTime(started) };
                    results[target.NodeIdentity] = result;
                    BroadcastingMetrics.RecordDelivery(
                        metrics,
                        typeof(TBroadcast),
                        result.Outcome,
                        result.Duration
                    );
                    if (logger is not null)
                    {
                        BroadcastingTypedLogger.LogDeliveryCompleted(
                            logger,
                            "UTL",
                            typeName,
                            target.NodeIdentity,
                            result.Outcome.ToString(),
                            result.Duration.Value.TotalMilliseconds
                        );
                    }

                    if (
                        !string.Equals(
                            target.NodeIdentity,
                            identity,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        try
                        {
                            await registry
                                .RecordDeliveryAsync(
                                    target.NodeIdentity,
                                    result.Outcome
                                        is not (
                                            BroadcastDeliveryOutcome.Failed
                                            or BroadcastDeliveryOutcome.Unreachable
                                            or BroadcastDeliveryOutcome.TimedOut
                                        ),
                                    result.Detail,
                                    token
                                )
                                .ConfigureAwait(false);
                        }
                        catch (Exception) when (!token.IsCancellationRequested)
                        {
                            // Reachability feedback must not alter the already observed delivery outcome.
                        }
                    }
                }
            )
            .ConfigureAwait(false);

        var broadcastResult = new BroadcastResult
        {
            BroadcastId = envelope.BroadcastId,
            TargetScopes = scopes,
            StartedUtc = now,
            CompletedUtc = timeProvider.GetUtcNow(),
            Nodes = results
                .Values.OrderBy(x => x.NodeIdentity, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
        BroadcastingMetrics.RecordPublication(
            metrics,
            typeof(TBroadcast),
            broadcastResult.TargetCount
        );
        if (logger is not null)
        {
            BroadcastingTypedLogger.LogPublicationCompleted(
                logger,
                "UTL",
                typeName,
                broadcastResult.TargetCount,
                broadcastResult.AcceptedCount,
                broadcastResult.FailureCount
            );
        }

        return Result<BroadcastResult>.Success(broadcastResult);
    }
}