// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Renders deterministic SVG for class diagrams.
/// </summary>
public class SvgClassDiagramRenderer : IDiagramRenderer
{
    private const string ArrowMarkerId = "arrow";
    private const string TriangleMarkerId = "triangle";
    private const string DiamondMarkerId = "diamond";
    private const string OpenDiamondMarkerId = "diamond-open";

    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != DiagramKind.Class)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the SVG class renderer.");
        }

        var svgOptions = SvgDiagramRendererSupport.CreateOptions(options);
        var orderedNodes = document.Nodes.ToList();
        var columns = Math.Max(1, Math.Min(3, (int)Math.Ceiling(Math.Sqrt(Math.Max(orderedNodes.Count, 1)))));
        var rects = CreateNodeRects(orderedNodes, columns, svgOptions);
        var canvas = CalculateCanvas(rects.Values, svgOptions);
        var outputWidth = svgOptions.Width ?? canvas.Width;
        var outputHeight = svgOptions.Height ?? canvas.Height;

        var builder = new StringBuilder();
        SvgDiagramRendererSupport.AppendDocumentStart(
            builder,
            svgOptions,
            outputWidth,
            outputHeight,
            "Class diagram",
            [
                SvgDiagramRendererSupport.CreateArrowMarker(ArrowMarkerId, svgOptions.StrokeColor),
                SvgDiagramRendererSupport.CreateTriangleMarker(TriangleMarkerId, svgOptions.StrokeColor),
                SvgDiagramRendererSupport.CreateDiamondMarker(DiamondMarkerId, svgOptions.StrokeColor, true),
                SvgDiagramRendererSupport.CreateDiamondMarker(OpenDiamondMarkerId, svgOptions.StrokeColor, false),
            ]);

        foreach (var edge in document.Edges)
        {
            if (rects.TryGetValue(edge.From, out var fromRect) && rects.TryGetValue(edge.To, out var toRect))
            {
                RenderEdge(builder, edge, fromRect, toRect, svgOptions);
            }
        }

        foreach (var node in orderedNodes)
        {
            RenderNode(builder, node, rects[node.Id], svgOptions);
        }

        SvgDiagramRendererSupport.AppendDocumentEnd(builder);
        return DiagramRenderResult.FromText(DiagramRenderFormat.Svg, builder.ToString(), SvgDiagramRendererSupport.ContentType);
    }

    private static Dictionary<string, SvgDiagramRendererSupport.Rect> CreateNodeRects(IReadOnlyList<DiagramNode> nodes, int columns, SvgDiagramRenderOptions options)
    {
        var rects = new Dictionary<string, SvgDiagramRendererSupport.Rect>(StringComparer.Ordinal);
        var columnWidth = options.NodeWidth + 40;
        var rowHeights = new Dictionary<int, double>();

        for (var index = 0; index < nodes.Count; index++)
        {
            var row = index / columns;
            rowHeights[row] = Math.Max(rowHeights.GetValueOrDefault(row), GetNodeHeight(nodes[index]));
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var x = options.Margin + (column * (columnWidth + options.HorizontalSpacing));
            var y = options.Margin + rowHeights.Where(x => x.Key < row).Sum(x => x.Value + options.VerticalSpacing);
            rects[nodes[index].Id] = new SvgDiagramRendererSupport.Rect(x, y, columnWidth, GetNodeHeight(nodes[index]));
        }

        return rects;
    }

    private static SvgDiagramRendererSupport.Size CalculateCanvas(IEnumerable<SvgDiagramRendererSupport.Rect> rects, SvgDiagramRenderOptions options)
    {
        if (!rects.Any())
        {
            return new SvgDiagramRendererSupport.Size(options.Margin * 2 + options.NodeWidth, options.Margin * 2 + options.NodeHeight);
        }

        return new SvgDiagramRendererSupport.Size(rects.Max(x => x.Right) + options.Margin, rects.Max(x => x.Bottom) + options.Margin);
    }

    private static double GetNodeHeight(DiagramNode node)
    {
        var titleLines = 1 + (string.IsNullOrWhiteSpace(ResolveStereotype(node)) ? 0 : 1);
        var memberLines = node.Members?.Count ?? 0;
        return 28 + (titleLines * 18) + (memberLines > 0 ? 14 + (memberLines * 18) : 0) + 20;
    }

    private static void RenderNode(StringBuilder builder, DiagramNode node, SvgDiagramRendererSupport.Rect rect, SvgDiagramRenderOptions options)
    {
        builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(rect.X)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(rect.Y)}\" width=\"{SvgDiagramRendererSupport.FormatNumber(rect.Width)}\" height=\"{SvgDiagramRendererSupport.FormatNumber(rect.Height)}\" rx=\"{options.CornerRadius}\" ry=\"{options.CornerRadius}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");

        var stereotype = ResolveStereotype(node);
        var titleHeight = 32 + (string.IsNullOrWhiteSpace(stereotype) ? 0 : 18);
        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(rect.X)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(rect.Y + titleHeight)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(rect.Right)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(rect.Y + titleHeight)}\" />");

        SvgDiagramRendererSupport.RenderText(builder, node.Label ?? node.Id, rect.Center.X, rect.Y + 22, options);
        if (!string.IsNullOrWhiteSpace(stereotype))
        {
            SvgDiagramRendererSupport.RenderText(builder, $"<<{stereotype}>>", rect.Center.X, rect.Y + 40, options);
        }

        if (node.Members?.Count > 0)
        {
            var baseline = rect.Y + titleHeight + 18;
            for (var i = 0; i < node.Members.Count; i++)
            {
                SvgDiagramRendererSupport.RenderText(builder, RenderMember(node.Members[i]), rect.X + 12, baseline + (i * 18), options, "start");
            }
        }
    }

    private static void RenderEdge(
        StringBuilder builder,
        DiagramEdge edge,
        SvgDiagramRendererSupport.Rect fromRect,
        SvgDiagramRendererSupport.Rect toRect,
        SvgDiagramRenderOptions options)
    {
        var from = GetBoundaryPoint(fromRect, toRect.Center);
        var to = GetBoundaryPoint(toRect, fromRect.Center);

        var dashArray = edge.Kind is DiagramEdgeKind.Dependency or DiagramEdgeKind.Realization ? " stroke-dasharray=\"6 4\"" : string.Empty;
        var markerStart = edge.Kind switch
        {
            DiagramEdgeKind.Composition => $" marker-start=\"url(#{DiamondMarkerId})\"",
            DiagramEdgeKind.Aggregation => $" marker-start=\"url(#{OpenDiamondMarkerId})\"",
            _ => string.Empty,
        };
        var markerEnd = edge.Kind switch
        {
            DiagramEdgeKind.Inheritance or DiagramEdgeKind.Realization => $" marker-end=\"url(#{TriangleMarkerId})\"",
            DiagramEdgeKind.Association or DiagramEdgeKind.Dependency or DiagramEdgeKind.Normal => $" marker-end=\"url(#{ArrowMarkerId})\"",
            _ => string.Empty,
        };

        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(from.X)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(from.Y)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(to.X)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(to.Y)}\"{markerStart}{markerEnd}{dashArray} />");

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            SvgDiagramRendererSupport.RenderText(builder, edge.Label, (from.X + to.X) / 2d, ((from.Y + to.Y) / 2d) - 8, options);
        }
    }

    private static SvgDiagramRendererSupport.Point GetBoundaryPoint(SvgDiagramRendererSupport.Rect rect, SvgDiagramRendererSupport.Point target)
    {
        var center = rect.Center;
        var deltaX = target.X - center.X;
        var deltaY = target.Y - center.Y;
        var scaleX = deltaX == 0 ? double.PositiveInfinity : (rect.Width / 2d) / Math.Abs(deltaX);
        var scaleY = deltaY == 0 ? double.PositiveInfinity : (rect.Height / 2d) / Math.Abs(deltaY);
        var scale = Math.Min(scaleX, scaleY);
        return new SvgDiagramRendererSupport.Point(center.X + (deltaX * scale), center.Y + (deltaY * scale));
    }

    private static string ResolveStereotype(DiagramNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Stereotype))
        {
            return node.Stereotype;
        }

        return node.Kind switch
        {
            DiagramNodeKind.Interface => "interface",
            DiagramNodeKind.AbstractClass => "abstract",
            DiagramNodeKind.Enum => "enumeration",
            _ => null,
        };
    }

    private static string RenderMember(DiagramNodeMember member)
    {
        var prefix = member.Visibility switch
        {
            DiagramVisibility.Public => "+",
            DiagramVisibility.Protected => "#",
            DiagramVisibility.Internal => "~",
            DiagramVisibility.Private => "-",
            _ => "+",
        };
        var name = member.Kind == DiagramMemberKind.Method && !member.Name.Contains('(', StringComparison.Ordinal)
            ? $"{member.Name}()"
            : member.Name;
        return string.IsNullOrWhiteSpace(member.Type)
            ? $"{prefix}{name}"
            : $"{prefix}{name}: {member.Type}";
    }
}