namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Opens the official bITdevKit documentation in a browser.
/// </summary>
public sealed class DocsConsoleCommand() : ConsoleCommandBase("docs", "Opens the official bITdevKit documentation")
{
    /// <summary>
    /// Gets the official bITdevKit documentation URL.
    /// </summary>
    public const string OfficialDocsUrl = "https://bridgingit-gmbh.github.io/bITdevKit/";

    /// <summary>
    /// Gets or sets a value indicating whether only the documentation URL should be written.
    /// </summary>
    [ConsoleCommandOption("url", Description = "Write the documentation URL without opening a browser.")]
    public bool UrlOnly { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var runtime = services.GetService<IDocsConsoleCommandRuntime>() ?? DocsConsoleCommandRuntime.Instance;
        var launcher = services.GetService<IDocsBrowserLauncher>() ?? ProcessDocsBrowserLauncher.Instance;
        var shouldOpen = !this.UrlOnly && runtime.CanOpenBrowser;
        var opened = false;

        if (shouldOpen)
        {
            try
            {
                launcher.Open(OfficialDocsUrl);
                opened = true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                runtime.Fail(console, $"Could not open the official documentation: {exception.Message}");
                return Task.CompletedTask;
            }
        }

        var result = new DocsConsoleCommandResult(OfficialDocsUrl, opened, 0);
        if (runtime.TryWriteResult(result))
        {
            return Task.CompletedTask;
        }

        if (this.UrlOnly || !opened)
        {
            console.WriteLine(OfficialDocsUrl);
            return Task.CompletedTask;
        }

        console.MarkupLine($"Opened official bITdevKit docs: [link={OfficialDocsUrl}]{OfficialDocsUrl}[/]");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Represents the result of the docs console command.
/// </summary>
/// <param name="Url">The official documentation URL.</param>
/// <param name="Opened">A value indicating whether a browser was opened.</param>
/// <param name="ExitCode">The process exit code associated with the command result.</param>
public sealed record DocsConsoleCommandResult(string Url, bool Opened, int ExitCode);

/// <summary>
/// Provides host-specific output and failure behavior for the docs console command.
/// </summary>
public interface IDocsConsoleCommandRuntime
{
    /// <summary>
    /// Gets a value indicating whether the command may open a browser.
    /// </summary>
    bool CanOpenBrowser { get; }

    /// <summary>
    /// Writes a host-specific command result when applicable.
    /// </summary>
    /// <param name="result">The command result.</param>
    /// <returns><see langword="true" /> when the result was handled by the runtime.</returns>
    bool TryWriteResult(DocsConsoleCommandResult result);

    /// <summary>
    /// Reports a command failure.
    /// </summary>
    /// <param name="console">The active console.</param>
    /// <param name="message">The failure message.</param>
    void Fail(IAnsiConsole console, string message);
}

/// <summary>
/// Opens URLs in the user's default browser.
/// </summary>
public interface IDocsBrowserLauncher
{
    /// <summary>
    /// Opens the supplied URL in the platform default browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    void Open(string url);
}

/// <summary>
/// Default docs command runtime for regular console command hosts.
/// </summary>
public sealed class DocsConsoleCommandRuntime : IDocsConsoleCommandRuntime
{
    /// <summary>
    /// Gets the default docs command runtime instance.
    /// </summary>
    public static DocsConsoleCommandRuntime Instance { get; } = new();

    /// <inheritdoc />
    public bool CanOpenBrowser => true;

    /// <inheritdoc />
    public bool TryWriteResult(DocsConsoleCommandResult result) => false;

    /// <inheritdoc />
    public void Fail(IAnsiConsole console, string message) => console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
}