// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents a persisted attachment row for a notification email.
/// </summary>
[Table("__Notifications_EmailAttachments")]
public class EmailMessageAttachmentEntity
{
    /// <summary>
    /// Gets or sets the attachment primary key.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent notification email identifier.
    /// </summary>
    [Required]
    public Guid EmailMessageId { get; set; }

    /// <summary>
    /// Gets or sets the original attachment file name.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the attachment media type.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the persisted attachment payload bytes.
    /// </summary>
    [Required]
    public byte[] Content { get; set; }

    /// <summary>
    /// Gets or sets the content identifier for inline attachments.
    /// </summary>
    [MaxLength(256)]
    public string ContentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the attachment should be rendered inline.
    /// </summary>
    public bool IsEmbedded { get; set; }

    /// <summary>
    /// Gets or sets the parent email navigation property.
    /// </summary>
    [ForeignKey(nameof(EmailMessageId))]
    public EmailMessageEntity EmailMessage { get; set; }
}
