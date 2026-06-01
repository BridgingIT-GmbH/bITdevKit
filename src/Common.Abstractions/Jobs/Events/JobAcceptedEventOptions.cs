// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Configures how an external event is accepted into the scheduler event-trigger pipeline.
/// </summary>
public sealed record JobAcceptedEventOptions
{
    private PropertyBag properties = new();

    /// <summary>
    /// Gets or sets the source-system identifier when available.
    /// </summary>
    public string SourceId { get; init; }

    /// <summary>
    /// Gets or sets the stable idempotency key used to deduplicate repeated accepts.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets or sets the propagated correlation identifier when available.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets optional immutable properties to persist alongside the accepted event.
    /// </summary>
    public PropertyBag Properties
    {
        get => this.properties;
        init => this.properties = value?.Clone() ?? new PropertyBag();
    }

    /// <summary>
    /// Gets or sets the UTC instant when the source accepted the event.
    /// </summary>
    public DateTimeOffset? AcceptedUtc { get; init; }
}