// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Represents the operational metadata for a persisted notification email attachment.
/// </summary>
public class NotificationEmailAttachmentInfo
{
    /// <summary>
    /// Gets or sets the attachment primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent notification email primary key.
    /// </summary>
    public Guid EmailMessageId { get; set; }

    /// <summary>
    /// Gets or sets the stored attachment file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the attachment media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the attachment byte length.
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// Gets or sets the optional content identifier for embedded attachments.
    /// </summary>
    public string ContentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the attachment is embedded inline.
    /// </summary>
    public bool IsEmbedded { get; set; }
}
