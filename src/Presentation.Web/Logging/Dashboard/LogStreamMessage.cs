// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

/// <summary>
/// Represents one text payload emitted by the dashboard log stream.
/// </summary>
/// <example>
/// <code>
/// var message = LogStreamMessage.Output("log line");
/// </code>
/// </example>
public sealed class LogStreamMessage
{
    /// <summary>
    /// Gets or sets the message kind, such as <c>output</c>, <c>status</c>, or <c>error</c>.
    /// </summary>
    public string Kind { get; set; } = "output";

    /// <summary>
    /// Gets or sets the ANSI text to write into the terminal.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the log entry id when this message represents a log entry.
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// Creates a regular output message.
    /// </summary>
    public static LogStreamMessage Output(string text, long? id = null)
    {
        return new LogStreamMessage { Kind = "output", Text = text, Id = id };
    }

    /// <summary>
    /// Creates a stream status message.
    /// </summary>
    public static LogStreamMessage Status(string text)
    {
        return new LogStreamMessage { Kind = "status", Text = text };
    }

    /// <summary>
    /// Creates a stream error message.
    /// </summary>
    public static LogStreamMessage Error(string text)
    {
        return new LogStreamMessage { Kind = "error", Text = text };
    }
}
