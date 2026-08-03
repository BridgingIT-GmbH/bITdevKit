// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using Spectre.Console.Rendering;

/// <summary>
/// Remaps Spectre.Console renderable styles to the currently selected DevKit console theme.
/// </summary>
/// <example>
/// <code>
/// console.Pipeline.Attach(new ConsoleThemeRenderHook());
/// </code>
/// </example>
public sealed class ConsoleThemeRenderHook : IRenderHook
{
    /// <inheritdoc />
    public IEnumerable<IRenderable> Process(RenderOptions options, IEnumerable<IRenderable> renderables)
    {
        ArgumentNullException.ThrowIfNull(renderables);

        return renderables.Select(renderable => renderable is ThemedRenderable ? renderable : new ThemedRenderable(renderable));
    }

    private static Segment Remap(Segment segment)
    {
        var style = Remap(segment.Style);
        return style == segment.Style ? segment : new Segment(segment.Text, style);
    }

    private static Style Remap(Style style)
    {
        if (style.Foreground == Color.Default)
        {
            return style;
        }

        var theme = ConsoleTheme.Current;
        var mapped = GetMappedStyle(theme, style.Foreground);
        if (mapped is null)
        {
            return style;
        }

        var target = Style.Parse(mapped);
        return new Style(
            target.Foreground,
            style.Background == Color.Default ? null : style.Background,
            target.Decoration | style.Decoration);
    }

    private static string GetMappedStyle(ConsoleThemePalette theme, Color color)
    {
        if (color == Color.Grey || color == Color.Silver)
        {
            return theme.MutedStyle;
        }

        if (color == Color.Cyan || color == Color.Aqua || color == Color.Blue || color == Color.Navy || color == Color.Purple || color == Color.Fuchsia)
        {
            return theme.AccentStyle;
        }

        if (color == Color.Yellow || color == Color.Olive)
        {
            return theme.WarningStyle;
        }

        if (color == Color.Red || color == Color.Maroon)
        {
            return theme.ErrorStyle;
        }

        if (color == Color.Green || color == Color.Lime)
        {
            return theme.InformationStyle;
        }

        return null;
    }

    private sealed class ThemedRenderable(IRenderable inner) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            return inner.Measure(options, maxWidth);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            return inner.Render(options, maxWidth).Select(Remap);
        }
    }
}
