// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a request to create a durable batch record.
/// </summary>
public class JobBatchCreateRequest
{
    private PropertyBag properties = new();

    /// <summary>
    /// Gets or sets the stable batch identifier.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the batch description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the completion policy.
    /// </summary>
    public JobBatchCompletionPolicy CompletionPolicy { get; set; } = JobBatchCompletionPolicy.RequireAllSucceeded;

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation identifier.
    /// </summary>
    public string CausationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets immutable batch properties.
    /// </summary>
    public PropertyBag Properties
    {
        get => this.properties;
        set => this.properties = value?.Clone() ?? new PropertyBag();
    }
}