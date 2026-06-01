// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents dispatch options for manual inline job execution.
/// </summary>
public sealed class JobDispatchOptions
{
    private PropertyBag properties = new();

    /// <summary>
    /// Gets or sets the target manual trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets optional immutable dispatch properties.
    /// </summary>
    public PropertyBag Properties
    {
        get => this.properties;
        set => this.properties = value?.Clone() ?? new PropertyBag();
    }

    /// <summary>
    /// Gets or sets a value indicating whether dispatch should create durable scheduler records.
    /// </summary>
    public bool Durable { get; set; } = true;
}