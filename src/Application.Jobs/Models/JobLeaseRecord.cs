// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents persisted lease properties for an occurrence.
/// </summary>
public sealed record JobLeaseRecord
{
    /// <summary>
    /// Gets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets the lease owner scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets the provider ownership token.
    /// </summary>
    public string OwnershipToken { get; init; }

    /// <summary>
    /// Gets the lease acquisition time.
    /// </summary>
    public DateTimeOffset AcquiredUtc { get; init; }

    /// <summary>
    /// Gets the last renewal time.
    /// </summary>
    public DateTimeOffset? RenewedUtc { get; init; }

    /// <summary>
    /// Gets the lease expiration time.
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; init; }

    /// <summary>
    /// Gets the renewal count.
    /// </summary>
    public int RenewalCount { get; init; }

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}