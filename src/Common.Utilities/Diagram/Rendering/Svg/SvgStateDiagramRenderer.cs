// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Globalization;
using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Renders deterministic SVG for state diagrams.
/// </summary>
public class SvgStateDiagramRenderer : IDiagramRenderer
{
    private const string StartOrEndIdentifier = "[*]";

    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != DiagramKind.State)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the SVG state renderer.");
        }

        var svgOptions = CreateOptions(options);
        var orderedNodeIds = GetOrderedNodeIds(document);
        var nodeMap = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var nodePositions = CreateNodePositions(orderedNodeIds, document.Direction, svgOptions);
        var canvas = CalculateCanvas(nodePositions, document.Notes, document.Direction, svgOptions);
        var scale = svgOptions.Scale ?? 1d;
        var outputWidth = svgOptions.Width ?? canvas.Width;
        var outputHeight = svgOptions.Height ?? canvas.Height;
        var scaledWidth = outputWidth * scale;
        var scaledHeight = outputHeight * scale;

        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
        builder.Append($"width=\"{FormatNumber(scaledWidth)}\" height=\"{FormatNumber(scaledHeight)}\" ");
        builder.Append($"viewBox=\"0 0 {FormatNumber(outputWidth)} {FormatNumber(outputHeight)}\" role=\"img\" aria-label=\"State diagram\">");

        builder.Append("<defs>");
        builder.Append($"<marker id=\"arrow\" markerWidth=\"10\" markerHeight=\"7\" refX=\"9\" refY=\"3.5\" orient=\"auto\"><polygon points=\"0 0, 10 3.5, 0 7\" fill=\"{EscapeAttribute(svgOptions.StrokeColor)}\" /></marker>");
        builder.Append("</defs>");

        if (!string.Equals(svgOptions.BackgroundColor, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append($"<rect x=\"0\" y=\"0\" width=\"{FormatNumber(outputWidth)}\" height=\"{FormatNumber(outputHeight)}\" fill=\"{EscapeAttribute(svgOptions.BackgroundColor)}\" />");
        }

        builder.Append($"<g font-family=\"{EscapeAttribute(svgOptions.FontFamily)}\" font-size=\"{svgOptions.FontSize}\" fill=\"{EscapeAttribute(svgOptions.TextColor)}\" stroke=\"{EscapeAttribute(svgOptions.StrokeColor)}\" stroke-width=\"1.5\">");

        foreach (var nodeId in orderedNodeIds)
        {
            var node = nodeMap.GetValueOrDefault(nodeId) ?? new DiagramNode(nodeId);
            RenderNode(builder, node, nodePositions[nodeId], svgOptions);
        }

        var startPosition = GetStartPosition(document.Direction, svgOptions, nodePositions, orderedNodeIds);
        var endPosition = GetEndPosition(document.Direction, svgOptions, nodePositions, orderedNodeIds);

        if (document.Edges.Any(edge => string.Equals(edge.From, StartOrEndIdentifier, StringComparison.Ordinal) || string.Equals(edge.To, StartOrEndIdentifier, StringComparison.Ordinal)))
        {
            RenderMarker(builder, startPosition, svgOptions);
            RenderMarker(builder, endPosition, svgOptions);
        }

        foreach (var edge in document.Edges)
        {
            RenderEdge(builder, edge, nodePositions, startPosition, endPosition, document.Direction, svgOptions);
        }

        foreach (var note in document.Notes)
        {
            RenderNote(builder, note, nodePositions, document.Direction, svgOptions);
        }

        builder.Append("</g></svg>");
        return DiagramRenderResult.FromText(DiagramRenderFormat.Svg, builder.ToString(), "image/svg+xml; charset=utf-8");
    }

    private static SvgDiagramRenderOptions CreateOptions(DiagramRenderOptions options)
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

    private static List<string> GetOrderedNodeIds(DiagramDocument document)
    {
        return document.Nodes.Select(node => node.Id)
            .Concat(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Concat(document.Notes.Select(note => note.TargetId))
            .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, StartOrEndIdentifier, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, Point> CreateNodePositions(IReadOnlyList<string> nodeIds, DiagramDirection direction, SvgDiagramRenderOptions options)
    {
        var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
        var horizontal = direction == DiagramDirection.LeftToRight;
        var baseX = options.Margin + options.MarkerRadius + (horizontal ? options.HorizontalSpacing : 0) + options.NodeWidth / 2d;
        var baseY = options.Margin + options.MarkerRadius + (!horizontal ? options.VerticalSpacing : 0) + options.NodeHeight / 2d;

        for (var i = 0; i < nodeIds.Count; i++)
        {
            positions[nodeIds[i]] = horizontal
                ? new Point(baseX + (i * (options.NodeWidth + options.HorizontalSpacing)), baseY)
                : new Point(baseX, baseY + (i * (options.NodeHeight + options.VerticalSpacing)));
        }

        return positions;
    }

    private static Size CalculateCanvas(IReadOnlyDictionary<string, Point> nodePositions, IReadOnlyList<DiagramNote> notes, DiagramDirection direction, SvgDiagramRenderOptions options)
    {
        if (nodePositions.Count == 0)
        {
            return new Size(options.Margin * 2 + options.NodeWidth, options.Margin * 2 + options.NodeHeight);
        }

        var maxX = nodePositions.Values.Max(point => point.X);
        var maxY = nodePositions.Values.Max(point => point.Y);
        var width = direction == DiagramDirection.LeftToRight
            ? maxX + options.NodeWidth / 2d + options.Margin + options.MarkerRadius
            : maxX + options.NodeWidth / 2d + options.Margin + (notes.Any(note => note.Position == DiagramNotePosition.Right) ? options.NoteWidth + options.HorizontalSpacing : 0);
        var height = direction == DiagramDirection.LeftToRight
            ? maxY + options.NodeHeight / 2d + options.Margin + (notes.Any(note => note.Position == DiagramNotePosition.Below) ? options.NoteHeight + options.VerticalSpacing : 0)
            : maxY + options.NodeHeight / 2d + options.Margin + options.MarkerRadius + options.VerticalSpacing;

        return new Size(width, height);
    }

    private static Point GetStartPosition(DiagramDirection direction, SvgDiagramRenderOptions options, IReadOnlyDictionary<string, Point> nodePositions, IReadOnlyList<string> nodeIds)
    {
        if (nodeIds.Count == 0)
        {
            return new Point(options.Margin + options.MarkerRadius, options.Margin + options.MarkerRadius);
        }

        var first = nodePositions[nodeIds[0]];
        return direction == DiagramDirection.LeftToRight
            ? new Point(first.X - (options.NodeWidth / 2d) - options.HorizontalSpacing / 2d, first.Y)
            : new Point(first.X, first.Y - (options.NodeHeight / 2d) - options.VerticalSpacing / 2d);
    }

    private static Point GetEndPosition(DiagramDirection direction, SvgDiagramRenderOptions options, IReadOnlyDictionary<string, Point> nodePositions, IReadOnlyList<string> nodeIds)
    {
        if (nodeIds.Count == 0)
        {
            return new Point(options.Margin + options.NodeWidth, options.Margin + options.NodeHeight);
        }

        var last = nodePositions[nodeIds[^1]];
        return direction == DiagramDirection.LeftToRight
            ? new Point(last.X + (options.NodeWidth / 2d) + options.HorizontalSpacing / 2d, last.Y)
            : new Point(last.X, last.Y + (options.NodeHeight / 2d) + options.VerticalSpacing / 2d);
    }

    private static void RenderNode(StringBuilder builder, DiagramNode node, Point center, SvgDiagramRenderOptions options)
    {
        var x = center.X - options.NodeWidth / 2d;
        var y = center.Y - options.NodeHeight / 2d;
        builder.Append($"<rect x=\"{FormatNumber(x)}\" y=\"{FormatNumber(y)}\" width=\"{options.NodeWidth}\" height=\"{options.NodeHeight}\" rx=\"{options.CornerRadius}\" ry=\"{options.CornerRadius}\" fill=\"{EscapeAttribute(options.FillColor)}\" />");
        RenderText(builder, node.Label ?? node.Id, center.X, center.Y + 5, options, "middle");
    }

    private static void RenderMarker(StringBuilder builder, Point center, SvgDiagramRenderOptions options)
    {
        builder.Append($"<circle cx=\"{FormatNumber(center.X)}\" cy=\"{FormatNumber(center.Y)}\" r=\"{options.MarkerRadius}\" fill=\"{EscapeAttribute(options.StrokeColor)}\" stroke=\"{EscapeAttribute(options.StrokeColor)}\" />");
    }

    private static void RenderEdge(
        StringBuilder builder,
        DiagramEdge edge,
        IReadOnlyDictionary<string, Point> nodePositions,
        Point startPosition,
        Point endPosition,
        DiagramDirection direction,
        SvgDiagramRenderOptions options)
    {
        var from = ResolveAnchor(edge.From, true, nodePositions, startPosition, endPosition, direction, options);
        var to = ResolveAnchor(edge.To, false, nodePositions, startPosition, endPosition, direction, options);
        builder.Append($"<line x1=\"{FormatNumber(from.X)}\" y1=\"{FormatNumber(from.Y)}\" x2=\"{FormatNumber(to.X)}\" y2=\"{FormatNumber(to.Y)}\" marker-end=\"url(#arrow)\" />");

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            var labelX = (from.X + to.X) / 2d;
            var labelY = (from.Y + to.Y) / 2d - 6;
            RenderText(builder, edge.Label, labelX, labelY, options, "middle");
        }
    }

    private static Point ResolveAnchor(
        string id,
        bool from,
        IReadOnlyDictionary<string, Point> nodePositions,
        Point startPosition,
        Point endPosition,
        DiagramDirection direction,
        SvgDiagramRenderOptions options)
    {
        if (string.Equals(id, StartOrEndIdentifier, StringComparison.Ordinal))
        {
            var marker = from ? startPosition : endPosition;
            return direction == DiagramDirection.LeftToRight
                ? new Point(marker.X + (from ? options.MarkerRadius : -options.MarkerRadius), marker.Y)
                : new Point(marker.X, marker.Y + (from ? options.MarkerRadius : -options.MarkerRadius));
        }

        var center = nodePositions[id];
        return direction == DiagramDirection.LeftToRight
            ? new Point(center.X + (from ? options.NodeWidth / 2d : -options.NodeWidth / 2d), center.Y)
            : new Point(center.X, center.Y + (from ? options.NodeHeight / 2d : -options.NodeHeight / 2d));
    }

    private static void RenderNote(StringBuilder builder, DiagramNote note, IReadOnlyDictionary<string, Point> nodePositions, DiagramDirection direction, SvgDiagramRenderOptions options)
    {
        if (!nodePositions.TryGetValue(note.TargetId, out var target))
        {
            return;
        }

        var noteCenter = note.Position switch
        {
            DiagramNotePosition.Left => new Point(target.X - (options.NodeWidth / 2d) - options.HorizontalSpacing / 2d - options.NoteWidth / 2d, target.Y),
            DiagramNotePosition.Above => new Point(target.X, target.Y - (options.NodeHeight / 2d) - options.VerticalSpacing / 2d - options.NoteHeight / 2d),
            DiagramNotePosition.Below => new Point(target.X, target.Y + (options.NodeHeight / 2d) + options.VerticalSpacing / 2d + options.NoteHeight / 2d),
            _ => new Point(target.X + (options.NodeWidth / 2d) + options.HorizontalSpacing / 2d + options.NoteWidth / 2d, target.Y),
        };

        var x = noteCenter.X - options.NoteWidth / 2d;
        var y = noteCenter.Y - options.NoteHeight / 2d;
        builder.Append($"<line x1=\"{FormatNumber(target.X)}\" y1=\"{FormatNumber(target.Y)}\" x2=\"{FormatNumber(noteCenter.X)}\" y2=\"{FormatNumber(noteCenter.Y)}\" stroke-dasharray=\"4 3\" />");
        builder.Append($"<rect x=\"{FormatNumber(x)}\" y=\"{FormatNumber(y)}\" width=\"{options.NoteWidth}\" height=\"{options.NoteHeight}\" rx=\"8\" ry=\"8\" fill=\"{EscapeAttribute(options.NoteFillColor)}\" stroke-dasharray=\"4 3\" />");
        RenderMultilineText(builder, note.Text, noteCenter.X, noteCenter.Y - 8, options);
    }

    private static void RenderMultilineText(StringBuilder builder, string text, double centerX, double baselineY, SvgDiagramRenderOptions options)
    {
        var lines = (text ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        builder.Append($"<text x=\"{FormatNumber(centerX)}\" y=\"{FormatNumber(baselineY)}\" text-anchor=\"middle\" fill=\"{EscapeAttribute(options.TextColor)}\">");
        for (var i = 0; i < lines.Length; i++)
        {
            var dy = i == 0 ? "0" : "1.2em";
            builder.Append($"<tspan x=\"{FormatNumber(centerX)}\" dy=\"{dy}\">{EscapeText(lines[i])}</tspan>");
        }

        builder.Append("</text>");
    }

    private static void RenderText(StringBuilder builder, string text, double x, double y, SvgDiagramRenderOptions options, string anchor)
    {
        builder.Append($"<text x=\"{FormatNumber(x)}\" y=\"{FormatNumber(y)}\" text-anchor=\"{anchor}\" fill=\"{EscapeAttribute(options.TextColor)}\">{EscapeText(text)}</text>");
    }

    private static string EscapeAttribute(string value)
    {
        return EscapeText(value).Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string EscapeText(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private readonly record struct Point(double X, double Y);

    private readonly record struct Size(double Width, double Height);
}