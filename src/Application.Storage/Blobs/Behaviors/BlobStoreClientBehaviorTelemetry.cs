// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Tracks per-operation blob-store behavior telemetry such as retry and timeout counts.
/// </summary>
/// <example>
/// <code>
/// using var scope = BlobStoreClientBehaviorTelemetry.Begin();
/// BlobStoreClientBehaviorTelemetry.IncrementRetry();
/// </code>
/// </example>
public static class BlobStoreClientBehaviorTelemetry
{
    private static readonly AsyncLocal<Scope> CurrentScope = new();

    /// <summary>
    /// Gets the current behavior telemetry scope for the async flow.
    /// </summary>
    /// <example>
    /// <code>
    /// var retries = BlobStoreClientBehaviorTelemetry.Current?.Retries;
    /// </code>
    /// </example>
    public static Scope Current => CurrentScope.Value;

    /// <summary>
    /// Begins a behavior telemetry scope for the current async flow.
    /// </summary>
    /// <returns>The created telemetry scope.</returns>
    /// <example>
    /// <code>
    /// using var scope = BlobStoreClientBehaviorTelemetry.Begin();
    /// </code>
    /// </example>
    public static Scope Begin()
    {
        var parent = CurrentScope.Value;
        var scope = new Scope(parent);
        CurrentScope.Value = scope;

        return scope;
    }

    /// <summary>
    /// Increments the retry count on the current telemetry scope.
    /// </summary>
    /// <example>
    /// <code>
    /// BlobStoreClientBehaviorTelemetry.IncrementRetry();
    /// </code>
    /// </example>
    public static void IncrementRetry() => CurrentScope.Value?.IncrementRetry();

    /// <summary>
    /// Increments the timeout count on the current telemetry scope.
    /// </summary>
    /// <example>
    /// <code>
    /// BlobStoreClientBehaviorTelemetry.IncrementTimeout();
    /// </code>
    /// </example>
    public static void IncrementTimeout() => CurrentScope.Value?.IncrementTimeout();

    /// <summary>
    /// Represents behavior telemetry captured for one async operation scope.
    /// </summary>
    /// <example>
    /// <code>
    /// using var scope = BlobStoreClientBehaviorTelemetry.Begin();
    /// var retries = scope.Retries;
    /// </code>
    /// </example>
    public sealed class Scope : IDisposable
    {
        private readonly Scope parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="Scope" /> class.
        /// </summary>
        /// <param name="parent">The parent telemetry scope to restore when this scope is disposed.</param>
        /// <example>
        /// <code>
        /// var scope = new BlobStoreClientBehaviorTelemetry.Scope(BlobStoreClientBehaviorTelemetry.Current);
        /// </code>
        /// </example>
        public Scope(Scope parent)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Gets the number of retry attempts recorded in this scope.
        /// </summary>
        /// <example>
        /// <code>
        /// var retries = scope.Retries;
        /// </code>
        /// </example>
        public long Retries { get; private set; }

        /// <summary>
        /// Gets the number of timeouts recorded in this scope.
        /// </summary>
        /// <example>
        /// <code>
        /// var timeouts = scope.Timeouts;
        /// </code>
        /// </example>
        public long Timeouts { get; private set; }

        /// <summary>
        /// Increments the retry count for this scope.
        /// </summary>
        /// <example>
        /// <code>
        /// scope.IncrementRetry();
        /// </code>
        /// </example>
        public void IncrementRetry() => this.Retries++;

        /// <summary>
        /// Increments the timeout count for this scope.
        /// </summary>
        /// <example>
        /// <code>
        /// scope.IncrementTimeout();
        /// </code>
        /// </example>
        public void IncrementTimeout() => this.Timeouts++;

        /// <inheritdoc />
        public void Dispose()
        {
            CurrentScope.Value = this.parent;
        }
    }
}
