// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("__Notifications_EmailAttachments")]
public class EmailMessageAttachmentEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid EmailMessageId { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; }

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; }

    [Required]
    public byte[] Content { get; set; }

    [MaxLength(256)]
    public string ContentId { get; set; }

    public bool IsEmbedded { get; set; }

    [ForeignKey(nameof(EmailMessageId))]
    public EmailMessageEntity EmailMessage { get; set; }
}
