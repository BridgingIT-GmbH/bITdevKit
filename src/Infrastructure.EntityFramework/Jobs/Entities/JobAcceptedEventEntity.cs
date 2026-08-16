// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents one durably accepted event waiting for event-trigger materialization.
/// </summary>
[Table("__Jobs_AcceptedEvents")]
[Index(nameof(Source), nameof(AcceptedUtc), nameof(AcceptedEventId))]
[Index(nameof(Source), nameof(IdempotencyKey), IsUnique = true)]
public class JobAcceptedEventEntity
{
    /// <summary>
    /// Gets or sets the accepted event id.
    /// </summary>
    [Key]
    public Guid AcceptedEventId { get; set; }

    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the serialized data.
    /// </summary>
    [Required]
    public string SerializedData { get; set; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string DataType { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the source id.
    /// </summary>
    [MaxLength(256)]
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the serialized properties.
    /// </summary>
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the accepted utc.
    /// </summary>
    [Required]
    public DateTimeOffset AcceptedUtc { get; set; }
}
