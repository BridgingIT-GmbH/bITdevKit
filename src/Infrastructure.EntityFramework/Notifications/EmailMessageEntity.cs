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
[Index(nameof(Status))]
[Index(nameof(Priority))]
[Index(nameof(CreatedAt))]
[Index(nameof(SentAt))]
public class EmailMessageEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string To
    {
        get => JsonSerializer.Serialize(this.to, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.to = value == null ? null : JsonSerializer.Deserialize<List<string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private List<string> to = [];

    public string CC
    {
        get => JsonSerializer.Serialize(this.cc, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.cc = value == null ? null : JsonSerializer.Deserialize<List<string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private List<string> cc = [];

    public string BCC
    {
        get => JsonSerializer.Serialize(this.bcc, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.bcc = value == null ? null : JsonSerializer.Deserialize<List<string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private List<string> bcc = [];

    [Required]
    public string From
    {
        get => JsonSerializer.Serialize(this.from, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.from = value == null ? null : JsonSerializer.Deserialize<EmailAddress>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private EmailAddress from;

    public string ReplyTo
    {
        get => this.replyTo == null ? null : JsonSerializer.Serialize(this.replyTo, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.replyTo = value == null ? null : JsonSerializer.Deserialize<EmailAddress>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private EmailAddress replyTo;

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; }

    [Required]
    public string Body { get; set; }

    public bool IsHtml { get; set; }

    public string Headers
    {
        get => JsonSerializer.Serialize(this.headers, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.headers = value == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private Dictionary<string, string> headers = [];

    [Column("Properties")]
    public string PropertiesJson
    {
        get => this.properties.IsNullOrEmpty() ? null : JsonSerializer.Serialize(this.properties, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.properties = value.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private Dictionary<string, object> properties = [];

    public EmailMessagePriority Priority { get; set; } = EmailMessagePriority.Normal;

    public EmailMessageStatus Status { get; set; } = EmailMessageStatus.Pending;

    public int RetryCount { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public List<EmailMessageAttachmentEntity> Attachments { get; set; } = [];
}
