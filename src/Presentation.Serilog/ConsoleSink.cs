// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.SystemConsole.Themes;
using System.Linq.Expressions;
using System.Reflection;
using SerilogConsoleTheme = Serilog.Sinks.SystemConsole.Themes.ConsoleTheme;
using SerilogConsoleThemeStyle = Serilog.Sinks.SystemConsole.Themes.ConsoleThemeStyle;

/// <summary>
/// Serilog sink that writes through Spectre.Console and preserves the DevKit interactive command prompt when one is active.
/// </summary>
/// <remarks>
/// The sink does not require interactive console commands to be registered. Without an active prompt session it behaves as a normal console sink.
/// </remarks>
/// <example>
/// <code>
/// loggerConfiguration.WriteTo.Console();
/// </code>
/// </example>
public sealed class ConsoleSink : ILogEventSink
{
    private const string DefaultOutputTemplate = "{Timestamp:HH:mm:ss.fff} {Level:u3} | {Message:lj}{NewLine}{Exception}";

    private readonly MessageTemplateTextFormatter formatter;
    private readonly InteractiveConsoleCoordinator coordinator;
    private readonly string outputTemplate;
    private readonly IFormatProvider formatProvider;
    private readonly bool colorize;
    private readonly object rendererSync = new();
    private object themedRenderer;
    private Action<object, LogEvent, TextWriter> themedFormatter;
    private string themedRendererThemeName;
    private bool themedRendererUnavailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleSink"/> class.
    /// </summary>
    /// <param name="outputTemplate">The Serilog output template.</param>
    /// <param name="formatProvider">The optional format provider.</param>
    /// <param name="coordinator">The optional interactive console coordinator.</param>
    /// <param name="colorize">A value indicating whether output should be colored by log level.</param>
    /// <example>
    /// <code>
    /// var sink = new ConsoleSink("{Message:lj}{NewLine}{Exception}");
    /// </code>
    /// </example>
    public ConsoleSink(
        string outputTemplate = null,
        IFormatProvider formatProvider = null,
        InteractiveConsoleCoordinator coordinator = null,
        bool colorize = true)
    {
        this.outputTemplate = string.IsNullOrWhiteSpace(outputTemplate) ? DefaultOutputTemplate : outputTemplate;
        this.formatProvider = formatProvider;
        this.formatter = new MessageTemplateTextFormatter(this.outputTemplate, formatProvider);
        this.coordinator = coordinator ?? InteractiveConsoleCoordinator.Instance;
        this.colorize = colorize;
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        using var writer = new StringWriter();
        if (!this.TryFormatThemed(logEvent, writer))
        {
            this.formatter.Format(logEvent, writer);
        }

        this.coordinator.Write(writer.ToString());
    }

    private bool TryFormatThemed(LogEvent logEvent, TextWriter writer)
    {
        if (!this.colorize || this.themedRendererUnavailable)
        {
            return false;
        }

        try
        {
            var (renderer, format) = this.GetThemedRenderer();
            format(renderer, logEvent, writer);
            return true;
        }
        catch
        {
            this.themedRendererUnavailable = true;
            return false;
        }
    }

    private (object Renderer, Action<object, LogEvent, TextWriter> Format) GetThemedRenderer()
    {
        if (this.themedRendererUnavailable)
        {
            throw new InvalidOperationException("The themed Serilog console renderer is unavailable.");
        }

        var currentTheme = ConsoleTheme.Current;
        if (this.themedRenderer is not null &&
            this.themedFormatter is not null &&
            string.Equals(this.themedRendererThemeName, currentTheme.Name, StringComparison.OrdinalIgnoreCase))
        {
            return (this.themedRenderer, this.themedFormatter);
        }

        lock (this.rendererSync)
        {
            if (this.themedRenderer is not null &&
                this.themedFormatter is not null &&
                string.Equals(this.themedRendererThemeName, currentTheme.Name, StringComparison.OrdinalIgnoreCase))
            {
                return (this.themedRenderer, this.themedFormatter);
            }

            var rendererType = typeof(SerilogConsoleTheme).Assembly.GetType("Serilog.Sinks.SystemConsole.Output.OutputTemplateRenderer", throwOnError: true);
            var renderer = Activator.CreateInstance(
                rendererType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [CreateSerilogTheme(currentTheme), this.outputTemplate, this.formatProvider],
                culture: null);
            var formatMethod = rendererType.GetMethod(
                "Format",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(LogEvent), typeof(TextWriter)],
                modifiers: null);
            var format = CreateFormatDelegate(rendererType, formatMethod);

            this.themedRenderer = renderer;
            this.themedFormatter = format;
            this.themedRendererThemeName = currentTheme.Name;

            return (renderer, format);
        }
    }

    private static Action<object, LogEvent, TextWriter> CreateFormatDelegate(Type rendererType, MethodInfo formatMethod)
    {
        if (formatMethod is null)
        {
            throw new MissingMethodException(rendererType.FullName, "Format");
        }

        var renderer = Expression.Parameter(typeof(object), "renderer");
        var logEvent = Expression.Parameter(typeof(LogEvent), "logEvent");
        var writer = Expression.Parameter(typeof(TextWriter), "writer");
        var call = Expression.Call(Expression.Convert(renderer, rendererType), formatMethod, logEvent, writer);

        return Expression.Lambda<Action<object, LogEvent, TextWriter>>(call, renderer, logEvent, writer).Compile();
    }

    private static SerilogConsoleTheme CreateSerilogTheme(ConsoleThemePalette theme)
    {
        var colors = CreateAnsiColors(theme.Name);
        return new AnsiConsoleTheme(new Dictionary<SerilogConsoleThemeStyle, string>
        {
            [SerilogConsoleThemeStyle.Text] = colors.Text,
            [SerilogConsoleThemeStyle.SecondaryText] = colors.Secondary,
            [SerilogConsoleThemeStyle.TertiaryText] = colors.Tertiary,
            [SerilogConsoleThemeStyle.Invalid] = colors.Error,
            [SerilogConsoleThemeStyle.Null] = colors.Scalar,
            [SerilogConsoleThemeStyle.Name] = colors.Name,
            [SerilogConsoleThemeStyle.String] = colors.String,
            [SerilogConsoleThemeStyle.Number] = colors.Number,
            [SerilogConsoleThemeStyle.Boolean] = colors.Scalar,
            [SerilogConsoleThemeStyle.Scalar] = colors.Scalar,
            [SerilogConsoleThemeStyle.LevelVerbose] = colors.Verbose,
            [SerilogConsoleThemeStyle.LevelDebug] = colors.Debug,
            [SerilogConsoleThemeStyle.LevelInformation] = colors.Information,
            [SerilogConsoleThemeStyle.LevelWarning] = colors.Warning,
            [SerilogConsoleThemeStyle.LevelError] = colors.Error,
            [SerilogConsoleThemeStyle.LevelFatal] = colors.Fatal
        });
    }

    private static AnsiColors CreateAnsiColors(string themeName) =>
        (themeName ?? string.Empty).ToLowerInvariant() switch
        {
            "catppuccin" => new(
                Text: Ansi256(189),
                Secondary: Ansi256(103),
                Tertiary: Ansi256(60),
                Name: Ansi256(111),
                String: Ansi256(217),
                Number: Ansi256(183),
                Scalar: Ansi256(223),
                Verbose: Ansi256(240),
                Debug: Ansi256(111),
                Information: Ansi256(189),
                Warning: Ansi256(229),
                Error: Ansi256(210),
                Fatal: Bold(Ansi256(210))),
            "darkforest" => new(
                Text: Ansi256(151),
                Secondary: Ansi256(65),
                Tertiary: Ansi256(22),
                Name: Ansi256(114),
                String: Ansi256(187),
                Number: Ansi256(215),
                Scalar: Ansi256(151),
                Verbose: Ansi256(238),
                Debug: Ansi256(107),
                Information: Ansi256(151),
                Warning: Ansi256(221),
                Error: Ansi256(203),
                Fatal: Bold(Ansi256(203))),
            "carbon" => new(
                Text: Ansi256(252),
                Secondary: Ansi256(244),
                Tertiary: Ansi256(238),
                Name: Ansi256(111),
                String: Ansi256(213),
                Number: Ansi256(81),
                Scalar: Ansi256(159),
                Verbose: Ansi256(240),
                Debug: Ansi256(111),
                Information: Ansi256(252),
                Warning: Ansi256(228),
                Error: Ansi256(204),
                Fatal: Bold(Ansi256(204))),
            "tokionights" => new(
                Text: Ansi256(189),
                Secondary: Ansi256(103),
                Tertiary: Ansi256(60),
                Name: Ansi256(111),
                String: Ansi256(176),
                Number: Ansi256(221),
                Scalar: Ansi256(159),
                Verbose: Ansi256(240),
                Debug: Ansi256(111),
                Information: Ansi256(189),
                Warning: Ansi256(221),
                Error: Ansi256(203),
                Fatal: Bold(Ansi256(203))),
            "matrix" => new(
                Text: Ansi256(46),
                Secondary: Ansi256(34),
                Tertiary: Ansi256(22),
                Name: Ansi256(118),
                String: Ansi256(82),
                Number: Ansi256(201),
                Scalar: Ansi256(46),
                Verbose: Ansi256(238),
                Debug: Ansi256(40),
                Information: Ansi256(46),
                Warning: Ansi256(226),
                Error: Ansi256(196),
                Fatal: Bold(Ansi256(196))),
            "light" => new(
                Text: Ansi256(232),
                Secondary: Ansi256(244),
                Tertiary: Ansi256(248),
                Name: Ansi256(27),
                String: Ansi256(127),
                Number: Ansi256(25),
                Scalar: Ansi256(25),
                Verbose: Ansi256(244),
                Debug: Ansi256(27),
                Information: Ansi256(232),
                Warning: Ansi256(130),
                Error: Ansi256(160),
                Fatal: Bold(Ansi256(160))),
            _ => new(
                Text: "\u001b[37m",
                Secondary: "\u001b[90m",
                Tertiary: "\u001b[90m",
                Name: "\u001b[36m",
                String: "\u001b[32m",
                Number: "\u001b[35m",
                Scalar: "\u001b[36m",
                Verbose: "\u001b[90m",
                Debug: "\u001b[36m",
                Information: "\u001b[37m",
                Warning: "\u001b[33m",
                Error: "\u001b[31m",
                Fatal: "\u001b[1;31m")
        };

    private static string Ansi256(int color) => $"\u001b[38;5;{color}m";

    private static string Bold(string color) => "\u001b[1m" + color;

    private sealed record AnsiColors(
        string Text,
        string Secondary,
        string Tertiary,
        string Name,
        string String,
        string Number,
        string Scalar,
        string Verbose,
        string Debug,
        string Information,
        string Warning,
        string Error,
        string Fatal);
}
