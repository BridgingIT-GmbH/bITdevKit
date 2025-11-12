// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using System;
using System.Collections.Generic;

/// <summary>
/// Data transfer object for representing a log entry in API responses.
/// </summary>
public class LogEntryModel
{
    /// <summary>
    /// Gets or sets the unique identifier for the log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the log message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the message template used for the log.
    /// </summary>
    public string MessageTemplate { get; set; }

    /// <summary>
    /// Gets or sets the log level (e.g., Information, Error).
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// Gets or sets the exception details, if any.
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// Gets or sets the additional properties as a dictionary.
    /// </summary>
    public IDictionary<string, object> LogEvents { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the trace identifier for distributed tracing.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier for distributed tracing.
    /// </summary>
    public string SpanId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for tracking related operations.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the custom log key for categorizing logs.
    /// </summary>
    public string LogKey { get; set; }

    /// <summary>
    /// Gets or sets the module name associated with the log entry.
    /// </summary>
    public string ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the thread ID associated with the log entry.
    /// </summary>
    public string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the short type name associated with the log entry.
    /// </summary>
    public string ShortTypeName { get; set; }
}
