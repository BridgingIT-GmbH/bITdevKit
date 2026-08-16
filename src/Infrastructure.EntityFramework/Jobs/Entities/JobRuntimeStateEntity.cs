// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents a durable runtime-state row for a registered job.
/// </summary>
[Table("__Jobs_RuntimeStates")]
public class JobRuntimeStateEntity
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [Key]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    [Required]
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the concurrency version.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Executes the advance concurrency version operation.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
