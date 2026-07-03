namespace BridgingIT.DevKit.Cli;

using System.Globalization;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Rendering;

/// <summary>
/// Renders the animated bdk CLI banner.
/// </summary>
public static class CliBannerService
{
    private static readonly Color Accent = new(33, 150, 243);
    private static readonly Color AccentDark = new(0, 96, 160);
    private static readonly Color AccentLight = new(144, 202, 249);
    private static readonly Color TextColor = Color.White;
    private static readonly Color BorderColor = Color.Grey;

    private static readonly string[] BannerLines =
    [
        "██████  ██████  ██   ██",
        "██   ██ ██   ██ ██  ██ ",
        "██████  ██   ██ █████  ",
        "██   ██ ██   ██ ██  ██ ",
        "██████  ██████  ██   ██"
    ];

    /// <summary>
    /// Displays the banner when invocation settings allow it.
    /// </summary>
    /// <param name="settings">The output settings.</param>
    /// <param name="assembly">The CLI assembly.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task DisplayIfEnabledAsync(CliOutputSettings settings, Assembly assembly, CancellationToken cancellationToken = default)
    {
        if (!ShouldDisplay(settings))
        {
            return;
        }

        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = settings.NoColor ? AnsiSupport.No : AnsiSupport.Detect,
            ColorSystem = settings.NoColor ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
            Out = new TextWriterAnsiConsoleOutput(System.Console.Error, 120, 32)
        });

        if (settings.NonInteractive || settings.NoColor || System.Console.IsErrorRedirected)
        {
            console.Write(CreatePanel(CreateBanner(version, BannerLines[0].Length)));
            console.WriteLine();
            return;
        }

        await console.Live(CreatePanel(new Text(string.Empty)))
            .AutoClear(false)
            .StartAsync(async context =>
            {
                context.UpdateTarget(CreatePanel(CreatePartialBanner(0, version)));
                await DelayAsync(70, cancellationToken).ConfigureAwait(false);

                for (var visibleColumns = 3; visibleColumns <= BannerLines[0].Length; visibleColumns += 3)
                {
                    context.UpdateTarget(CreatePanel(CreatePartialBanner(visibleColumns, version)));
                    await DelayAsync(45, cancellationToken).ConfigureAwait(false);
                }

                for (var shineColumn = 0; shineColumn <= BannerLines[0].Length; shineColumn += 2)
                {
                    context.UpdateTarget(CreatePanel(CreateBannerWithShine(version, shineColumn)));
                    await DelayAsync(25, cancellationToken).ConfigureAwait(false);
                }

                context.UpdateTarget(CreatePanel(CreateBanner(version, BannerLines[0].Length)));
            }).ConfigureAwait(false);
        console.WriteLine();
    }

    private static bool ShouldDisplay(CliOutputSettings settings)
    {
        if (settings.NoLogo)
        {
            return false;
        }

        if (settings.Banner)
        {
            return true;
        }

        return !settings.Quiet &&
            !settings.IsJson &&
            !settings.IsCi &&
            !settings.NonInteractive &&
            !System.Console.IsInputRedirected &&
            !System.Console.IsOutputRedirected &&
            !System.Console.IsErrorRedirected;
    }

    private static Panel CreatePanel(IRenderable content)
        => new Panel(content)
            .Border(BoxBorder.Rounded)
            .BorderColor(BorderColor)
            .Padding(2, 1);

    private static Rows CreatePartialBanner(int visibleColumns, string version)
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[rgb({TextColor.R},{TextColor.G},{TextColor.B})]BridgingIT DevKit[/]")
        };

        foreach (var line in BannerLines)
        {
            var partial = line[..Math.Min(visibleColumns, line.Length)].PadRight(line.Length);
            rows.Add(new Markup(BuildLineMarkup(partial, -1)));
        }

        rows.Add(new Markup($"[grey]{string.Format(CultureInfo.InvariantCulture, "bdk {0}", version).EscapeMarkup()}[/]"));
        return new Rows(rows);
    }

    private static Rows CreateBannerWithShine(string version, int shineColumn)
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[rgb({TextColor.R},{TextColor.G},{TextColor.B})]BridgingIT DevKit[/]")
        };

        foreach (var line in BannerLines)
        {
            rows.Add(new Markup(BuildLineMarkup(line, shineColumn)));
        }

        rows.Add(new Markup($"[grey]{string.Format(CultureInfo.InvariantCulture, "bdk {0}", version).EscapeMarkup()}[/]"));
        return new Rows(rows);
    }

    private static Rows CreateBanner(string version, int visibleColumns)
        => CreatePartialBanner(visibleColumns, version);

    private static string BuildLineMarkup(string line, int shineColumn)
    {
        var markup = string.Empty;
        for (var column = 0; column < line.Length; column++)
        {
            var character = line[column];
            if (character == ' ')
            {
                markup += ' ';
                continue;
            }

            var color = Accent;
            if (shineColumn >= 0 && column >= shineColumn && column < shineColumn + 3)
            {
                color = AccentLight;
            }
            else if (column % 2 == 0)
            {
                color = AccentDark;
            }

            markup += $"[rgb({color.R},{color.G},{color.B})]{character}[/]";
        }

        return markup;
    }

    private static async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}

/// <summary>
/// Spectre.Console output adapter backed by a text writer.
/// </summary>
/// <param name="writer">The writer to receive rendered output.</param>
/// <param name="width">The output width.</param>
/// <param name="height">The output height.</param>
public sealed class TextWriterAnsiConsoleOutput(TextWriter writer, int width, int height) : IAnsiConsoleOutput
{
    /// <inheritdoc />
    public TextWriter Writer { get; } = writer;

    /// <inheritdoc />
    public bool IsTerminal => true;

    /// <inheritdoc />
    public int Width { get; } = width;

    /// <inheritdoc />
    public int Height { get; } = height;

    /// <inheritdoc />
    public void SetEncoding(System.Text.Encoding encoding)
    {
    }
}