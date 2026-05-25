// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
internal static class SvgFlowchartDiagramRenderer
{
    private const string ArrowMarkerId = "arrow";

    public static DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options, DiagramKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != expectedKind)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the SVG {expectedKind.ToString().ToLowerInvariant()} renderer.");
        }

        var svgOptions = SvgDiagramRendererSupport.CreateOptions(options);
        var orderedNodeIds = GetOrderedNodeIds(document);
        var nodeMap = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var nodePositions = CreateNodePositions(orderedNodeIds, document.Direction, svgOptions);
        var groupBounds = CreateGroupBounds(document.Groups, nodePositions, svgOptions);
        var noteRects = CreateNoteRects(document.Notes, nodePositions, svgOptions);
        var canvas = CalculateCanvas(nodePositions, groupBounds.Values, noteRects.Values, svgOptions);
        var outputWidth = svgOptions.Width ?? canvas.Width;
        var outputHeight = svgOptions.Height ?? canvas.Height;

        var builder = new StringBuilder();
        SvgDiagramRendererSupport.AppendDocumentStart(
            builder,
            svgOptions,
            outputWidth,
            outputHeight,
            $"{expectedKind} diagram",
            [SvgDiagramRendererSupport.CreateArrowMarker(ArrowMarkerId, svgOptions.StrokeColor)]);

        foreach (var group in document.Groups)
        {
            if (groupBounds.TryGetValue(group.Id, out var bounds))
            {
                RenderGroup(builder, group, bounds, svgOptions);
            }
        }

        foreach (var edge in document.Edges)
        {
            RenderEdge(builder, edge, nodeMap, nodePositions, document.Direction, svgOptions);
        }

        foreach (var nodeId in orderedNodeIds)
        {
            var node = nodeMap.GetValueOrDefault(nodeId) ?? new DiagramNode(nodeId);
            RenderNode(builder, node, nodePositions[nodeId], expectedKind, svgOptions);
        }

        foreach (var note in document.Notes)
        {
            if (noteRects.TryGetValue(note, out var rect) && nodePositions.TryGetValue(note.TargetId, out var target))
            {
                RenderNote(builder, note, rect, target, svgOptions);
            }
        }

        SvgDiagramRendererSupport.AppendDocumentEnd(builder);
        return DiagramRenderResult.FromText(DiagramRenderFormat.Svg, builder.ToString(), SvgDiagramRendererSupport.ContentType);
    }

    private static List<string> GetOrderedNodeIds(DiagramDocument document)
    {
        return document.Nodes.Select(node => node.Id)
            .Concat(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Concat(document.Notes.Select(note => note.TargetId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, SvgDiagramRendererSupport.Point> CreateNodePositions(
        IReadOnlyList<string> nodeIds,
        DiagramDirection direction,
        SvgDiagramRenderOptions options)
    {
        var positions = new Dictionary<string, SvgDiagramRendererSupport.Point>(StringComparer.Ordinal);
        var horizontal = direction == DiagramDirection.LeftToRight;
        var baseX = options.Margin + options.NoteWidth + options.NodeWidth / 2d;
        var baseY = options.Margin + options.NodeHeight / 2d + 24;

        for (var index = 0; index < nodeIds.Count; index++)
        {
            positions[nodeIds[index]] = horizontal
                ? new SvgDiagramRendererSupport.Point(baseX + (index * (options.NodeWidth + options.HorizontalSpacing)), baseY)
                : new SvgDiagramRendererSupport.Point(baseX, baseY + (index * (options.NodeHeight + options.VerticalSpacing)));
        }

        return positions;
    }

    private static Dictionary<string, SvgDiagramRendererSupport.Rect> CreateGroupBounds(
        IReadOnlyList<DiagramGroup> groups,
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> nodePositions,
        SvgDiagramRenderOptions options)
    {
        var result = new Dictionary<string, SvgDiagramRendererSupport.Rect>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var points = group.NodeIds
                .Where(nodePositions.ContainsKey)
                .Select(id => nodePositions[id])
                .ToList();

            if (points.Count == 0)
            {
                continue;
            }

            var left = points.Min(point => point.X) - (options.NodeWidth / 2d) - 20;
            var top = points.Min(point => point.Y) - (options.NodeHeight / 2d) - 32;
            var right = points.Max(point => point.X) + (options.NodeWidth / 2d) + 20;
            var bottom = points.Max(point => point.Y) + (options.NodeHeight / 2d) + 20;
            result[group.Id] = new SvgDiagramRendererSupport.Rect(left, top, right - left, bottom - top);
        }

        return result;
    }

    private static Dictionary<DiagramNote, SvgDiagramRendererSupport.Rect> CreateNoteRects(
        IReadOnlyList<DiagramNote> notes,
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> nodePositions,
        SvgDiagramRenderOptions options)
    {
        var result = new Dictionary<DiagramNote, SvgDiagramRendererSupport.Rect>();
        foreach (var note in notes)
        {
            if (!nodePositions.TryGetValue(note.TargetId, out var target))
            {
                continue;
            }

            var center = note.Position switch
            {
                DiagramNotePosition.Left => new SvgDiagramRendererSupport.Point(target.X - (options.NodeWidth / 2d) - options.HorizontalSpacing / 2d - options.NoteWidth / 2d, target.Y),
                DiagramNotePosition.Above => new SvgDiagramRendererSupport.Point(target.X, target.Y - (options.NodeHeight / 2d) - options.VerticalSpacing / 2d - options.NoteHeight / 2d),
                DiagramNotePosition.Below => new SvgDiagramRendererSupport.Point(target.X, target.Y + (options.NodeHeight / 2d) + options.VerticalSpacing / 2d + options.NoteHeight / 2d),
                _ => new SvgDiagramRendererSupport.Point(target.X + (options.NodeWidth / 2d) + options.HorizontalSpacing / 2d + options.NoteWidth / 2d, target.Y),
            };

            result[note] = new SvgDiagramRendererSupport.Rect(
                center.X - (options.NoteWidth / 2d),
                center.Y - (options.NoteHeight / 2d),
                options.NoteWidth,
                options.NoteHeight);
        }

        return result;
    }

    private static SvgDiagramRendererSupport.Size CalculateCanvas(
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> nodePositions,
        IEnumerable<SvgDiagramRendererSupport.Rect> groupRects,
        IEnumerable<SvgDiagramRendererSupport.Rect> noteRects,
        SvgDiagramRenderOptions options)
    {
        if (nodePositions.Count == 0)
        {
            return new SvgDiagramRendererSupport.Size(options.Margin * 2 + options.NodeWidth, options.Margin * 2 + options.NodeHeight);
        }

        var left = nodePositions.Values.Min(point => point.X) - (options.NodeWidth / 2d) - options.Margin;
        var top = nodePositions.Values.Min(point => point.Y) - (options.NodeHeight / 2d) - options.Margin;
        var right = nodePositions.Values.Max(point => point.X) + (options.NodeWidth / 2d) + options.Margin;
        var bottom = nodePositions.Values.Max(point => point.Y) + (options.NodeHeight / 2d) + options.Margin;

        foreach (var rect in groupRects.Concat(noteRects))
        {
            left = Math.Min(left, rect.Left - options.Margin / 2d);
            top = Math.Min(top, rect.Top - options.Margin / 2d);
            right = Math.Max(right, rect.Right + options.Margin / 2d);
            bottom = Math.Max(bottom, rect.Bottom + options.Margin / 2d);
        }

        return new SvgDiagramRendererSupport.Size(right - Math.Min(left, 0), bottom - Math.Min(top, 0));
    }

    private static void RenderGroup(StringBuilder builder, DiagramGroup group, SvgDiagramRendererSupport.Rect bounds, SvgDiagramRenderOptions options)
    {
        builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(bounds.X)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(bounds.Y)}\" width=\"{SvgDiagramRendererSupport.FormatNumber(bounds.Width)}\" height=\"{SvgDiagramRendererSupport.FormatNumber(bounds.Height)}\" rx=\"12\" ry=\"12\" fill=\"transparent\" stroke-dasharray=\"8 6\" />");
        SvgDiagramRendererSupport.RenderText(builder, group.Label ?? group.Id, bounds.X + 12, bounds.Y + 20, options, "start");
    }

    private static void RenderEdge(
        StringBuilder builder,
        DiagramEdge edge,
        IReadOnlyDictionary<string, DiagramNode> nodeMap,
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> nodePositions,
        DiagramDirection direction,
        SvgDiagramRenderOptions options)
    {
        if (!nodePositions.TryGetValue(edge.From, out var fromCenter) || !nodePositions.TryGetValue(edge.To, out var toCenter))
        {
            return;
        }

        var from = ResolveAnchor(fromCenter, nodeMap.GetValueOrDefault(edge.From)?.Kind ?? DiagramNodeKind.Normal, direction, true, options);
        var to = ResolveAnchor(toCenter, nodeMap.GetValueOrDefault(edge.To)?.Kind ?? DiagramNodeKind.Normal, direction, false, options);
        var dashArray = edge.Kind is DiagramEdgeKind.Dependency or DiagramEdgeKind.Realization or DiagramEdgeKind.Reply ? " stroke-dasharray=\"6 4\"" : string.Empty;

        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(from.X)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(from.Y)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(to.X)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(to.Y)}\" marker-end=\"url(#{ArrowMarkerId})\"{dashArray} />");

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            var labelX = (from.X + to.X) / 2d;
            var labelY = (from.Y + to.Y) / 2d - 8;
            SvgDiagramRendererSupport.RenderText(builder, edge.Label, labelX, labelY, options);
        }
    }

    private static SvgDiagramRendererSupport.Point ResolveAnchor(
        SvgDiagramRendererSupport.Point center,
        DiagramNodeKind kind,
        DiagramDirection direction,
        bool from,
        SvgDiagramRenderOptions options)
    {
        var width = options.NodeWidth / 2d;
        var height = options.NodeHeight / 2d;

        if (kind is DiagramNodeKind.Decision or DiagramNodeKind.Branch or DiagramNodeKind.Join)
        {
            width = options.NodeWidth / 2d;
            height = options.NodeHeight / 2d;
        }

        return direction == DiagramDirection.LeftToRight
            ? new SvgDiagramRendererSupport.Point(center.X + (from ? width : -width), center.Y)
            : new SvgDiagramRendererSupport.Point(center.X, center.Y + (from ? height : -height));
    }

    private static void RenderNode(
        StringBuilder builder,
        DiagramNode node,
        SvgDiagramRendererSupport.Point center,
        DiagramKind diagramKind,
        SvgDiagramRenderOptions options)
    {
        var x = center.X - (options.NodeWidth / 2d);
        var y = center.Y - (options.NodeHeight / 2d);

        switch (node.Kind)
        {
            case DiagramNodeKind.Start:
                builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(x)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(y)}\" width=\"{options.NodeWidth}\" height=\"{options.NodeHeight}\" rx=\"{options.NodeHeight / 2d}\" ry=\"{options.NodeHeight / 2d}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
                break;
            case DiagramNodeKind.Terminal:
                builder.Append($"<ellipse cx=\"{SvgDiagramRendererSupport.FormatNumber(center.X)}\" cy=\"{SvgDiagramRendererSupport.FormatNumber(center.Y)}\" rx=\"{options.NodeWidth / 2d}\" ry=\"{options.NodeHeight / 2d}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
                break;
            case DiagramNodeKind.Decision:
            case DiagramNodeKind.Branch:
            case DiagramNodeKind.Join:
                builder.Append($"<polygon points=\"{SvgDiagramRendererSupport.FormatNumber(center.X)} {SvgDiagramRendererSupport.FormatNumber(y)}, {SvgDiagramRendererSupport.FormatNumber(x + options.NodeWidth)} {SvgDiagramRendererSupport.FormatNumber(center.Y)}, {SvgDiagramRendererSupport.FormatNumber(center.X)} {SvgDiagramRendererSupport.FormatNumber(y + options.NodeHeight)}, {SvgDiagramRendererSupport.FormatNumber(x)} {SvgDiagramRendererSupport.FormatNumber(center.Y)}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
                break;
            case DiagramNodeKind.Database:
                RenderDatabaseNode(builder, x, y, options);
                break;
            default:
                builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(x)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(y)}\" width=\"{options.NodeWidth}\" height=\"{options.NodeHeight}\" rx=\"{options.CornerRadius}\" ry=\"{options.CornerRadius}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
                if (node.Kind == DiagramNodeKind.Component)
                {
                    var tabX = x + 12;
                    builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(tabX)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(y + 10)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(tabX)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(y + options.NodeHeight - 10)}\" />");
                    builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(tabX + 10)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(y + 10)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(tabX + 10)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(y + options.NodeHeight - 10)}\" />");
                }
                break;
        }

        var text = BuildNodeText(node, diagramKind);
        SvgDiagramRendererSupport.RenderMultilineText(builder, text, center.X, center.Y - ((SvgDiagramRendererSupport.SplitLines(text).Count - 1) * 8), options);
    }

    private static void RenderDatabaseNode(StringBuilder builder, double x, double y, SvgDiagramRenderOptions options)
    {
        var ellipseHeight = Math.Min(16, options.NodeHeight / 3d);
        var cx = x + (options.NodeWidth / 2d);
        builder.Append($"<ellipse cx=\"{SvgDiagramRendererSupport.FormatNumber(cx)}\" cy=\"{SvgDiagramRendererSupport.FormatNumber(y + ellipseHeight)}\" rx=\"{options.NodeWidth / 2d}\" ry=\"{SvgDiagramRendererSupport.FormatNumber(ellipseHeight)}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
        builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(x)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(y + ellipseHeight)}\" width=\"{options.NodeWidth}\" height=\"{SvgDiagramRendererSupport.FormatNumber(options.NodeHeight - (ellipseHeight * 2))}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
        builder.Append($"<ellipse cx=\"{SvgDiagramRendererSupport.FormatNumber(cx)}\" cy=\"{SvgDiagramRendererSupport.FormatNumber(y + options.NodeHeight - ellipseHeight)}\" rx=\"{options.NodeWidth / 2d}\" ry=\"{SvgDiagramRendererSupport.FormatNumber(ellipseHeight)}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
    }

    private static string BuildNodeText(DiagramNode node, DiagramKind diagramKind)
    {
        var label = node.Label ?? node.Id;
        return diagramKind == DiagramKind.Component && !string.IsNullOrWhiteSpace(node.Stereotype)
            ? $"{label}\n<<{node.Stereotype}>>"
            : label;
    }

    private static void RenderNote(
        StringBuilder builder,
        DiagramNote note,
        SvgDiagramRendererSupport.Rect rect,
        SvgDiagramRendererSupport.Point target,
        SvgDiagramRenderOptions options)
    {
        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(target.X)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(target.Y)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(rect.Center.X)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(rect.Center.Y)}\" stroke-dasharray=\"4 3\" />");
        builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(rect.X)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(rect.Y)}\" width=\"{SvgDiagramRendererSupport.FormatNumber(rect.Width)}\" height=\"{SvgDiagramRendererSupport.FormatNumber(rect.Height)}\" rx=\"8\" ry=\"8\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.NoteFillColor)}\" stroke-dasharray=\"4 3\" />");
        SvgDiagramRendererSupport.RenderMultilineText(builder, note.Text, rect.Center.X, rect.Center.Y - 8, options);
    }
}