// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes one protected source-handle acquisition without retaining keys, dimensions or request principals.</summary>
/// <param name="Collection">The declared collection alias.</param>
/// <param name="Provider">The declared provider alias.</param>
/// <param name="Partial">Whether this acquisition selects a partial logical representation.</param>
/// <example><code>var context = new CacheTransferContext("CARDS", "DATABASE", false);</code></example>
public sealed record CacheTransferContext(string Collection, string Provider, bool Partial);

/// <summary>Reports source integrity completion independently of any destination transport outcome.</summary>
/// <param name="Context">The bounded acquisition context.</param>
/// <param name="Completion">The actual source-handle terminal result, including incomplete disposal.</param>
/// <param name="BytesDelivered">The logical bytes delivered by this source handle.</param>
/// <param name="Elapsed">The monotonic duration since acquisition.</param>
/// <example><code>if (observation.Completion.IsSuccess) { RecordCompleted(observation.BytesDelivered); }</code></example>
public sealed record CacheTransferObservation(CacheTransferContext Context, Result<CacheReadCompletion> Completion, long BytesDelivered, TimeSpan Elapsed);

/// <summary>Optionally starts one observation scope for each acquired protected source handle.</summary>
/// <remarks>Register this interface on a configured collection behavior. Implementations must not read, dispose or retain the consumer's stream.</remarks>
/// <example><code>public ICacheTransferObservationScope BeginTransfer(CacheTransferContext context) => new TransferScope(context);</code></example>
public interface ICacheTransferObserver
{
    /// <summary>Starts optional acquisition-to-terminal observation, or returns null when disabled.</summary>
    /// <param name="context">Only predeclared aliases and the selected integrity scope.</param>
    /// <returns>A per-transfer observation scope, or null.</returns>
    ICacheTransferObservationScope BeginTransfer(CacheTransferContext context);
}

/// <summary>Receives exactly one terminal source outcome. Observer failures cannot change the actual cache result.</summary>
/// <example><code>public void Complete(CacheTransferObservation observation) => Write(observation.Completion.IsSuccess);</code></example>
public interface ICacheTransferObservationScope
{
    /// <summary>Observes completed integrity, an explicit read failure, or early disposal without draining the source.</summary>
    /// <param name="observation">The actual terminal outcome and bytes delivered so far.</param>
    void Complete(CacheTransferObservation observation);
}