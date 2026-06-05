// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using System.Collections.Concurrent;

/// <summary>
/// Stores active dashboard web console sessions.
/// </summary>
/// <example>
/// <code>
/// var session = sessions.GetOrCreate("browser-session");
/// </code>
/// </example>
public sealed class WebConsoleSessionManager
{
    private readonly ConcurrentDictionary<string, WebConsoleSession> sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or creates a web console session.
    /// </summary>
    public WebConsoleSession GetOrCreate(string sessionId)
    {
        return this.sessions.GetOrAdd(sessionId, id => new WebConsoleSession(id));
    }

    /// <summary>
    /// Gets the number of known sessions.
    /// </summary>
    public int Count => this.sessions.Count;
}
