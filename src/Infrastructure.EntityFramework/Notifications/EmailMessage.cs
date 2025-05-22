namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BridgingIT.DevKit.Application.Notifications;

[Table("__Outbox_Emails")]
[Index(nameof(Status))]
[Index(nameof(Priority))]
[Index(nameof(CreatedAt))]
[Index(nameof(SentAt))]
public class EmailMessage
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string To
    {
        get => JsonSerializer.Serialize(_to, DefaultSystemTextJsonSerializerOptions.Create());
        set => _to = JsonSerializer.Deserialize<List<string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private List<string> _to = new();

    public string CC
    {
        get => JsonSerializer.Serialize(_cc, DefaultSystemTextJsonSerializerOptions.Create());
        set => _cc = JsonSerializer.Deserialize<List<string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private List<string> _cc = new();

    public string BCC
    {
        get => JsonSerializer.Serialize(_bcc, DefaultSystemTextJsonSerializerOptions.Create());
        set => _bcc = JsonSerializer.Deserialize<List<string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private List<string> _bcc = new();

    [Required]
    public string From
    {
        get => JsonSerializer.Serialize(_from, DefaultSystemTextJsonSerializerOptions.Create());
        set => _from = JsonSerializer.Deserialize<Application.Notifications.EmailAddress>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private Application.Notifications.EmailAddress _from;

    public string ReplyTo
    {
        get => _replyTo == null ? null : JsonSerializer.Serialize(_replyTo, DefaultSystemTextJsonSerializerOptions.Create());
        set => _replyTo = value == null ? null : JsonSerializer.Deserialize<Application.Notifications.EmailAddress>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private Application.Notifications.EmailAddress _replyTo;

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; }

    [Required]
    public string Body { get; set; }

    public bool IsHtml { get; set; }

    public string Headers
    {
        get => JsonSerializer.Serialize(_headers, DefaultSystemTextJsonSerializerOptions.Create());
        set => _headers = JsonSerializer.Deserialize<Dictionary<string, string>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private Dictionary<string, string> _headers = new();

    [Column("Properties")]
    public string PropertiesJson
    {
        get => _properties.IsNullOrEmpty() ? null : JsonSerializer.Serialize(_properties, DefaultSystemTextJsonSerializerOptions.Create());
        set => _properties = value.IsNullOrEmpty() ? new() : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
    private Dictionary<string, object> _properties = new();

    public EmailPriority Priority { get; set; } = EmailPriority.Normal;

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int RetryCount { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public List<EmailAttachment> Attachments { get; set; } = new();
}

[Table("__EmailMessage_Attachments")]
public class EmailAttachment
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
    public EmailMessage EmailMessage { get; set; }
}
