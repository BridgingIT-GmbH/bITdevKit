// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>
/// Provides shared console behavior for Storage feature commands.
/// </summary>
/// <example>
/// <code>
/// storage blobs list --client default --container reports
/// </code>
/// </example>
public abstract class StorageConsoleCommandBase(string name, string description, params string[] aliases)
    : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <inheritdoc />
    public string GroupName => "storage";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => [];

    /// <summary>
    /// Writes Result-native failure details to the console.
    /// </summary>
    /// <param name="console">The console to write to.</param>
    /// <param name="result">The failed result.</param>
    /// <example>
    /// <code>
    /// WriteErrors(console, result);
    /// </code>
    /// </example>
    protected static void WriteErrors(IAnsiConsole console, IResult result)
    {
        var messages = result.Errors.SafeNull()
            .Select(error => error.Message)
            .Concat(result.Messages.SafeNull())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (messages.Length == 0)
        {
            console.MarkupLine("[red]Storage operation failed.[/]");
            return;
        }

        foreach (var message in messages)
        {
            console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }
    }

    /// <summary>
    /// Writes a short command-specific usage hint.
    /// </summary>
    /// <param name="console">The console to write to.</param>
    /// <param name="usage">The command usage text.</param>
    /// <example>
    /// <code>
    /// WriteUsage(console, "storage files list --provider default");
    /// </code>
    /// </example>
    protected static void WriteUsage(IAnsiConsole console, string usage)
    {
        console.MarkupLine($"[yellow]Usage:[/] {Markup.Escape(usage)}");
    }

    /// <summary>
    /// Writes a provider-neutral continuation token hint without interpreting the token.
    /// </summary>
    /// <param name="console">The console to write to.</param>
    /// <param name="continuationToken">The opaque continuation token.</param>
    /// <example>
    /// <code>
    /// WriteContinuation(console, page.ContinuationToken);
    /// </code>
    /// </example>
    protected static void WriteContinuation(IAnsiConsole console, string continuationToken)
    {
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            console.MarkupLine($"[grey]Next continuation token:[/] {Markup.Escape(continuationToken)}");
        }
    }
}
