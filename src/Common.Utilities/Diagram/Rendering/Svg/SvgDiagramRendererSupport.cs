// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Globalization;
using System.Text;

namespace BridgingIT.DevKit.Common;
internal static class SvgDiagramRendererSupport
{
    internal const string ContentType = "image/svg+xml; charset=utf-8";

    internal static SvgDiagramRenderOptions CreateOptions(DiagramRenderOptions options)
    {
        if (options is SvgDiagramRenderOptions svgOptions)
        {
            return svgOptions;
        }

        return new SvgDiagramRenderOptions
        {
            IncludeHeader = options?.IncludeHeader ?? true,
            Width = options?.Width,
            Height = options?.Height,
            Scale = options?.Scale,
            Theme = options?.Theme,
        };
    }

    internal static void AppendDocumentStart(
        StringBuilder builder,
        SvgDiagramRenderOptions options,
        double outputWidth,
        double outputHeight,
        string ariaLabel,
        IEnumerable<string> markerDefinitions)
    {
        var scale = options.Scale ?? 1d;
        var scaledWidth = outputWidth * scale;
        var scaledHeight = outputHeight * scale;

        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
        builder.Append($"width=\"{FormatNumber(scaledWidth)}\" height=\"{FormatNumber(scaledHeight)}\" ");
        builder.Append($"viewBox=\"0 0 {FormatNumber(outputWidth)} {FormatNumber(outputHeight)}\" role=\"img\" aria-label=\"{EscapeAttribute(ariaLabel)}\">");

        var definitions = markerDefinitions?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        if (definitions.Count > 0)
        {
            builder.Append("<defs>");
            foreach (var definition in definitions)
            {
                builder.Append(definition);
            }

            builder.Append("</defs>");
        }

        if (!string.Equals(options.BackgroundColor, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append($"<rect x=\"0\" y=\"0\" width=\"{FormatNumber(outputWidth)}\" height=\"{FormatNumber(outputHeight)}\" fill=\"{EscapeAttribute(options.BackgroundColor)}\" />");
        }

        builder.Append($"<g font-family=\"{EscapeAttribute(options.FontFamily)}\" font-size=\"{options.FontSize}\" fill=\"{EscapeAttribute(options.TextColor)}\" stroke=\"{EscapeAttribute(options.StrokeColor)}\" stroke-width=\"1.5\">");
    }

    internal static void AppendDocumentEnd(StringBuilder builder)
    {
        builder.Append("</g></svg>");
    }

    internal static string CreateArrowMarker(string id, string color)
    {
        return $"<marker id=\"{EscapeAttribute(id)}\" markerWidth=\"10\" markerHeight=\"7\" refX=\"9\" refY=\"3.5\" orient=\"auto\"><polygon points=\"0 0, 10 3.5, 0 7\" fill=\"{EscapeAttribute(color)}\" /></marker>";
    }

    internal static string CreateTriangleMarker(string id, string strokeColor)
    {
        return $"<marker id=\"{EscapeAttribute(id)}\" markerWidth=\"14\" markerHeight=\"12\" refX=\"12\" refY=\"6\" orient=\"auto\"><polygon points=\"0 0, 12 6, 0 12\" fill=\"#ffffff\" stroke=\"{EscapeAttribute(strokeColor)}\" stroke-width=\"1.3\" /></marker>";
    }

    internal static string CreateDiamondMarker(string id, string strokeColor, bool filled)
    {
        var fill = filled ? EscapeAttribute(strokeColor) : "#ffffff";
        return $"<marker id=\"{EscapeAttribute(id)}\" markerWidth=\"14\" markerHeight=\"14\" refX=\"7\" refY=\"7\" orient=\"auto\"><polygon points=\"7 0, 14 7, 7 14, 0 7\" fill=\"{fill}\" stroke=\"{EscapeAttribute(strokeColor)}\" stroke-width=\"1.2\" /></marker>";
    }

    internal static void RenderText(StringBuilder builder, string text, double x, double y, SvgDiagramRenderOptions options, string anchor = "middle")
    {
        builder.Append($"<text x=\"{FormatNumber(x)}\" y=\"{FormatNumber(y)}\" text-anchor=\"{anchor}\" fill=\"{EscapeAttribute(options.TextColor)}\">{EscapeText(text)}</text>");
    }

    internal static void RenderMultilineText(
        StringBuilder builder,
        string text,
        double x,
        double y,
        SvgDiagramRenderOptions options,
        string anchor = "middle")
    {
        var lines = SplitLines(text);
        builder.Append($"<text x=\"{FormatNumber(x)}\" y=\"{FormatNumber(y)}\" text-anchor=\"{anchor}\" fill=\"{EscapeAttribute(options.TextColor)}\">");
        for (var i = 0; i < lines.Count; i++)
        {
            builder.Append($"<tspan x=\"{FormatNumber(x)}\" dy=\"{(i == 0 ? "0" : "1.2em")}\">{EscapeText(lines[i])}</tspan>");
        }

        builder.Append("</text>");
    }

    internal static IReadOnlyList<string> SplitLines(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n');
    }

    internal static string EscapeAttribute(string value)
    {
        return EscapeText(value).Replace("'", "&apos;", StringComparison.Ordinal);
    }

    internal static string EscapeText(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    internal static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    internal readonly record struct Point(double X, double Y);

    internal readonly record struct Size(double Width, double Height);

    internal readonly record struct Rect(double X, double Y, double Width, double Height)
    {
        public double Left => this.X;

        public double Top => this.Y;

        public double Right => this.X + this.Width;

        public double Bottom => this.Y + this.Height;

        public Point Center => new(this.X + (this.Width / 2d), this.Y + (this.Height / 2d));
    }
}