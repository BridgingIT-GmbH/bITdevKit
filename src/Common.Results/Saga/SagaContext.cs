// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides contextual information for saga execution including correlation, causation, and custom metadata.
/// Used for distributed tracing, logging, and debugging across saga boundaries.
/// </summary>
public class SagaContext
{
    /// <summary>
    /// Gets the unique identifier for this saga instance.
    /// Used to correlate all operations and compensations within a single saga execution.
    /// </summary>
    public string SagaId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the correlation identifier that tracks related operations across multiple sagas or services.
    /// Typically represents the original request/command that initiated the workflow.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets a dictionary of custom metadata associated with this saga.
    /// Can store arbitrary context information like user IDs, tenant IDs, or business identifiers.
    /// </summary>
    public PropertyBag Properties { get; } = [];

    /// <summary>
    /// Sets a metadata value in the saga context.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void SetProperty(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        this.Properties[key] = value;
    }

    /// <summary>
    /// Gets a metadata value from the saga context.
    /// </summary>
    /// <typeparam name="T">The type of the metadata value.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <returns>The metadata value if found; otherwise, default(T).</returns>
    public T GetProperty<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return this.Properties.TryGet<T>(key, out var value)
            ? value
            : default;
    }
}