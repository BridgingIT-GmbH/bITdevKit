// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using System.IO;
using System.Text;

/// <summary>
/// Text writer that forwards Spectre.Console output to a SignalR caller.
/// </summary>
/// <example>
/// <code>
/// var writer = new SignalRTextWriter((text, token) => SendAsync(text), token);
/// </code>
/// </example>
public sealed class SignalRTextWriter(
    Func<string, CancellationToken, Task> sendAsync,
    CancellationToken cancellationToken) : TextWriter
{
    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <inheritdoc />
    public override void Write(char value)
    {
        this.Write(value.ToString());
    }

    /// <inheritdoc />
    public override void Write(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        sendAsync(value, cancellationToken).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override Task WriteAsync(string value)
    {
        return string.IsNullOrEmpty(value)
            ? Task.CompletedTask
            : sendAsync(value, cancellationToken);
    }
}
