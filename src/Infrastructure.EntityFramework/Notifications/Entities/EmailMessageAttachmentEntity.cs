// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("__Notifications_EmailAttachments")]
/// <summary>
/// Represents a persisted attachment row for a notification email.
/// </summary>
public class EmailMessageAttachmentEntity
{
    [Key]
    /// <summary>
    /// Gets or sets the attachment primary key.
    /// </summary>
    public Guid Id { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the parent notification email identifier.
    /// </summary>
    public Guid EmailMessageId { get; set; }

    [Required]
    [MaxLength(255)]
    /// <summary>
    /// Gets or sets the original attachment file name.
    /// </summary>
    public string FileName { get; set; }

    [Required]
    [MaxLength(100)]
    /// <summary>
    /// Gets or sets the attachment media type.
    /// </summary>
    public string ContentType { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the persisted attachment payload bytes.
    /// </summary>
    public byte[] Content { get; set; }

    [MaxLength(256)]
    /// <summary>
    /// Gets or sets the content identifier for inline attachments.
    /// </summary>
    public string ContentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the attachment should be rendered inline.
    /// </summary>
    public bool IsEmbedded { get; set; }

    [ForeignKey(nameof(EmailMessageId))]
    /// <summary>
    /// Gets or sets the parent email navigation property.
    /// </summary>
    public EmailMessageEntity EmailMessage { get; set; }
}
