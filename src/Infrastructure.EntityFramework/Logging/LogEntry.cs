// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a log entry entity for storing Serilog events in a database table.
/// This entity is configured to work with the Serilog.Sinks.MSSqlServer sink,
/// mapping to a table named '__Logging_LogEntries' with standard columns as defined by the sink.
/// The entity is database-neutral, compatible with EF Core providers such as
/// SQL Server, SQLite, PostgreSQL, and MySQL.
/// Indexes are applied to optimize common query patterns (e.g., filtering by Level, TimeStamp, or TraceId).
/// </summary>
[Table("__Logging_LogEntries")]
[Index(nameof(LogKey), Name = "IX_Logging_LogEntries_LogKey")]
[Index(nameof(Level), Name = "IX_Logging_LogEntries_Level")]
[Index(nameof(TimeStamp), Name = "IX_Logging_LogEntries_TimeStamp")]
[Index(nameof(TraceId), Name = "IX_Logging_LogEntries_TraceId")]
[Index(nameof(CorrelationId), Name = "IX_Logging_LogEntries_CorrelationId")]
[Index(nameof(ModuleName), Name = "IX_Logging_LogEntries_ModuleName")]
//[Index(nameof(ThreadId), Name = "IX_Logging_LogEntries_ThreadId")]
[Index(nameof(ShortTypeName), Name = "IX_Logging_LogEntries_ShortTypeName")]
//[Index(nameof(Message), Name = "IX_Logging_LogEntries_Message")]
[Index(nameof(IsArchived), Name = "IX_Logging_LogEntries_IsArchived")]
public class LogEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the log entry (auto-increment).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the log message.
    /// </summary>
    //[MaxLength(4000)]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the message template used for the log.
    /// </summary>
    //[MaxLength(4000)]
    public string MessageTemplate { get; set; }

    /// <summary>
    /// Gets or sets the log level (e.g., Information, Error).
    /// </summary>
    [MaxLength(50)]
    public string Level { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    //[Column(TypeName = "datetime")]
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// Gets or sets the exception details, if any.
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// Gets or sets the trace identifier for distributed tracing.
    /// </summary>
    [MaxLength(128)]
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier for distributed tracing.
    /// </summary>
    [MaxLength(128)]
    public string SpanId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for tracking related operations.
    /// </summary>
    [MaxLength(128)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the custom log key for categorizing logs.
    /// </summary>
    [MaxLength(128)]
    public string LogKey { get; set; }

    /// <summary>
    /// Gets or sets whether the log entry is archived.
    /// Null or false indicates active logs, true indicates archived logs.
    /// </summary>
    public bool? IsArchived { get; set; } = false;

    /// <summary>
    /// Gets or sets the module name associated with the log entry.
    /// </summary>
    [MaxLength(128)]
    public string ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the thread ID associated with the log entry.
    /// </summary>
    [MaxLength(128)]
    public string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the short type name associated with the log entry.
    /// </summary>
    [MaxLength(128)]
    public string ShortTypeName { get; set; }

    /// <summary>
    /// Gets or sets the log event details as a dictionary (not stored directly).
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> LogEvents { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the JSON representation of the log event.
    /// </summary>
    [Column("LogEvent")]
    public string LogEventsJson
    {
        get => this.LogEvents.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(this.LogEvents, DefaultSystemTextJsonSerializerOptions.Create());
        set => this.LogEvents = value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultSystemTextJsonSerializerOptions.Create());
    }
}
