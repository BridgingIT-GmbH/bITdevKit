// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents a log entry entity for storing Serilog events in a database table.
/// This entity is configured to work with the Serilog.Sinks.MSSqlServer sink,
/// mapping to a table named '__Logging_LogEntries' with standard columns as defined by the sink.
/// The entity is database-neutral, compatible with EF Core providers such as
/// SQL Server, SQLite, PostgreSQL, and MySQL.
/// Indexes are applied to optimize common query patterns (e.g., filtering by Level, TimeStamp, or TraceId).
/// </summary>
[Table("__Logging_LogEntries")]
[Index(nameof(Level), Name = "IX_LogEntries_Level")]
[Index(nameof(TimeStamp), Name = "IX_LogEntries_TimeStamp")]
[Index(nameof(TraceId), Name = "IX_LogEntries_TraceId")]
public class LogEntry
{
    /// <summary>
    /// Gets or sets the primary key and auto-incrementing identifier for the log entry.
    /// Maps to an auto-incrementing column (e.g., IDENTITY in SQL Server, SERIAL in PostgreSQL).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the formatted log message with property placeholders replaced.
    /// Required and stored as a text-based column (e.g., nvarchar(max) in SQL Server).
    /// </summary>
    [Required]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the log message template containing property placeholders.
    /// Required and stored as a text-based column (e.g., nvarchar(max) in SQL Server).
    /// </summary>
    [Required]
    public string MessageTemplate { get; set; }

    /// <summary>
    /// Gets or sets the log event level (e.g., 'Error', 'Information').
    /// Required and limited to 16 characters for efficiency.
    /// Stored as a fixed-length string (e.g., nvarchar(16) in SQL Server).
    /// Indexed to optimize filtering by log level.
    /// </summary>
    [Required]
    [MaxLength(16)]
    public string Level { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the log event.
    /// Required and stored as a date/time type (e.g., datetime2 in SQL Server, timestamp in PostgreSQL).
    /// Indexed to optimize date range queries and sorting.
    /// </summary>
    [Required]
    public DateTime TimeStamp { get; set; }

    /// <summary>
    /// Gets or sets the exception message associated with the log event, if any.
    /// Optional and stored as a nullable text-based column (e.g., nvarchar(max) NULL in SQL Server).
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// Gets or sets the XML representation of log event properties.
    /// Optional and stored as a nullable text-based column (e.g., nvarchar(max) NULL in SQL Server).
    /// Typically used when JSON-based LogEvent is not enabled.
    /// </summary>
    public string Properties { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of log event properties.
    /// Optional and stored as a nullable text-based column (e.g., nvarchar(max) NULL in SQL Server).
    /// Enabled by adding StandardColumn.LogEvent to ColumnOptions.Store in the Serilog sink.
    /// </summary>
    public string LogEvent { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry TraceId for distributed tracing.
    /// Optional and stored as a nullable text-based column (e.g., nvarchar(max) NULL in SQL Server).
    /// Enabled by adding StandardColumn.TraceId to ColumnOptions.Store in the Serilog sink.
    /// Indexed to optimize tracing-related queries.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry SpanId for specific operations within a trace.
    /// Optional and stored as a nullable text-based column (e.g., nvarchar(max) NULL in SQL Server).
    /// Enabled by adding StandardColumn.SpanId to ColumnOptions.Store in the Serilog sink.
    /// </summary>
    public string SpanId { get; set; }
}
