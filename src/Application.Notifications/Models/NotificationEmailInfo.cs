// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Represents the operational view of a persisted notification email.
/// </summary>
public class NotificationEmailInfo
{
    /// <summary>
    /// Gets or sets the notification email primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the primary recipients.
    /// </summary>
    public ICollection<string> To { get; set; } = [];

    /// <summary>
    /// Gets or sets the carbon-copy recipients.
    /// </summary>
    public ICollection<string> Cc { get; set; } = [];

    /// <summary>
    /// Gets or sets the blind-carbon-copy recipients.
    /// </summary>
    public ICollection<string> Bcc { get; set; } = [];

    /// <summary>
    /// Gets or sets the sender display name.
    /// </summary>
    public string FromName { get; set; }

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the reply-to display name.
    /// </summary>
    public string ReplyToName { get; set; }

    /// <summary>
    /// Gets or sets the reply-to email address.
    /// </summary>
    public string ReplyToAddress { get; set; }

    /// <summary>
    /// Gets or sets the message subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the body is HTML.
    /// </summary>
    public bool IsHtml { get; set; }

    /// <summary>
    /// Gets or sets the notification priority.
    /// </summary>
    public EmailMessagePriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the outbox processing status.
    /// </summary>
    public EmailMessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of outbox processing attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the send timestamp for successfully processed mail.
    /// </summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the notification email has been archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the notification email was archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the lease owner for the current outbox claim.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the latest processing summary persisted by the outbox worker.
    /// </summary>
    public string ProcessMessage { get; set; }

    /// <summary>
    /// Gets or sets the attachment count.
    /// </summary>
    public int AttachmentCount { get; set; }

    /// <summary>
    /// Gets or sets the attachment metadata.
    /// </summary>
    public ICollection<NotificationEmailAttachmentInfo> Attachments { get; set; } = [];

    /// <summary>
    /// Gets or sets the persisted email properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
