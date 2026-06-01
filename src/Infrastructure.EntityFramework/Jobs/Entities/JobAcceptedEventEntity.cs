// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_AcceptedEvents")]
[Index(nameof(Source), nameof(AcceptedUtc), nameof(AcceptedEventId))]
[Index(nameof(Source), nameof(IdempotencyKey), IsUnique = true)]
/// <summary>
/// Represents one durably accepted event waiting for event-trigger materialization.
/// </summary>
public class JobAcceptedEventEntity
{
    [Key]
    public Guid AcceptedEventId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Source { get; set; }

    [Required]
    public string SerializedData { get; set; }

    [Required]
    [MaxLength(1024)]
    public string DataType { get; set; }

    [Required]
    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    [MaxLength(256)]
    public string SourceId { get; set; }

    [MaxLength(256)]
    public string CorrelationId { get; set; }

    public string SerializedProperties { get; set; }

    [Required]
    public DateTimeOffset AcceptedUtc { get; set; }
}
