// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

/// <summary>
/// Defines the filters and polling settings used by the dashboard log stream.
/// </summary>
/// <example>
/// <code>
/// var request = new LogStreamRequest { Level = "Information", LogKey = "REQ" };
/// </code>
/// </example>
public sealed class LogStreamRequest
{
    /// <summary>
    /// Gets or sets the minimum log level. Use <c>All</c> to disable level filtering.
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    /// Gets or sets the log key filter.
    /// </summary>
    public string LogKey { get; set; }
}
