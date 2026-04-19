// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BridgingIT.DevKit.Application.Notifications;

[Table("__Notifications_Emails")]
[Index(nameof(IsArchived), nameof(Status), nameof(LockedUntil), nameof(CreatedAt))]
[Index(nameof(IsArchived), nameof(Priority), nameof(CreatedAt))]
[Index(nameof(IsArchived), nameof(SentAt))]
[Index(nameof(IsArchived), nameof(ArchivedDate))]
/// <summary>
/// Represents a persisted notification email row managed by the Entity Framework outbox.
/// </summary>
/// <remarks>
/// The entity stores transport metadata, serialized recipient/header collections, attachments, lease state,
/// and soft-archive information so operators can remove rows from the active working set without purging them.
/// </remarks>
public class EmailMessageEntity
{
    [Key]
    /// <summary>
    /// Gets or sets the primary key for the persisted notification email.
    /// </summary>
    public Guid Id { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the serialized primary-recipient collection.
    /// </summary>
    public string To
    {
        get => JsonSerializer.Serialize(this.to, DefaultJsonSerializerOptions.Create());
        set => this.to = value == null ? null : JsonSerializer.Deserialize<List<string>>(value, DefaultJsonSerializerOptions.Create());
    }

    private List<string> to = [];

    /// <summary>
    /// Gets or sets the serialized carbon-copy recipient collection.
    /// </summary>
    public string CC
    {
        get => JsonSerializer.Serialize(this.cc, DefaultJsonSerializerOptions.Create());
        set => this.cc = value == null ? null : JsonSerializer.Deserialize<List<string>>(value, DefaultJsonSerializerOptions.Create());
    }

    private List<string> cc = [];

    /// <summary>
    /// Gets or sets the serialized blind-carbon-copy recipient collection.
    /// </summary>
    public string BCC
    {
        get => JsonSerializer.Serialize(this.bcc, DefaultJsonSerializerOptions.Create());
        set => this.bcc = value == null ? null : JsonSerializer.Deserialize<List<string>>(value, DefaultJsonSerializerOptions.Create());
    }

    private List<string> bcc = [];

    [Required]
    /// <summary>
    /// Gets or sets the serialized sender identity.
    /// </summary>
    public string From
    {
        get => JsonSerializer.Serialize(this.from, DefaultJsonSerializerOptions.Create());
        set => this.from = value == null ? null : JsonSerializer.Deserialize<EmailAddress>(value, DefaultJsonSerializerOptions.Create());
    }

    private EmailAddress from;

    /// <summary>
    /// Gets or sets the serialized reply-to identity.
    /// </summary>
    public string ReplyTo
    {
        get => this.replyTo == null ? null : JsonSerializer.Serialize(this.replyTo, DefaultJsonSerializerOptions.Create());
        set => this.replyTo = value == null ? null : JsonSerializer.Deserialize<EmailAddress>(value, DefaultJsonSerializerOptions.Create());
    }

    private EmailAddress replyTo;

    [Required]
    [MaxLength(500)]
    /// <summary>
    /// Gets or sets the message subject line.
    /// </summary>
    public string Subject { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the persisted email body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Body"/> contains HTML markup.
    /// </summary>
    public bool IsHtml { get; set; }

    /// <summary>
    /// Gets or sets the serialized transport-header dictionary.
    /// </summary>
    public string Headers
    {
        get => JsonSerializer.Serialize(this.headers, DefaultJsonSerializerOptions.Create());
        set => this.headers = value == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(value, DefaultJsonSerializerOptions.Create());
    }

    private Dictionary<string, string> headers = [];

    [Column("Properties")]
    /// <summary>
    /// Gets or sets the JSON persistence column for arbitrary email properties.
    /// </summary>
    public string PropertiesJson
    {
        get => this.properties.IsNullOrEmpty() ? null : JsonSerializer.Serialize(this.properties, DefaultJsonSerializerOptions.Create());
        set => this.properties = value.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultJsonSerializerOptions.Create());
    }

    private Dictionary<string, object> properties = [];

    /// <summary>
    /// Gets or sets the delivery priority.
    /// </summary>
    public EmailMessagePriority Priority { get; set; } = EmailMessagePriority.Normal;

    /// <summary>
    /// Gets or sets the outbox processing status.
    /// </summary>
    public EmailMessageStatus Status { get; set; } = EmailMessageStatus.Pending;

    /// <summary>
    /// Gets or sets the number of outbox processing attempts performed so far.
    /// </summary>
    public int RetryCount { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the timestamp when the email row was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when delivery succeeded.
    /// </summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the email has been archived out of the active working set.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the email was archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    [MaxLength(256)]
    /// <summary>
    /// Gets or sets the worker instance that currently owns the outbox lease.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    [Required]
    [ConcurrencyCheck]
    /// <summary>
    /// Gets or sets the provider-neutral optimistic concurrency token.
    /// </summary>
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the persisted attachment rows for this email.
    /// </summary>
    public List<EmailMessageAttachmentEntity> Attachments { get; set; } = [];

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
