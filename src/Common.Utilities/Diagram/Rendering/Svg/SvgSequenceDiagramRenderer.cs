// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Renders deterministic SVG for sequence diagrams.
/// </summary>
public class SvgSequenceDiagramRenderer : IDiagramRenderer
{
    private const string ArrowMarkerId = "arrow";

    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != DiagramKind.Sequence)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the SVG sequence renderer.");
        }

        var svgOptions = SvgDiagramRendererSupport.CreateOptions(options);
        var participantIds = GetOrderedParticipantIds(document);
        var participantMap = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var participantWidth = svgOptions.NodeWidth;
        var participantHeight = svgOptions.NodeHeight;
        var baseX = svgOptions.Margin + svgOptions.NoteWidth + (participantWidth / 2d);
        var baseY = svgOptions.Margin + (participantHeight / 2d);
        var participantCenters = participantIds
            .Select((id, index) => new KeyValuePair<string, SvgDiagramRendererSupport.Point>(id, new SvgDiagramRendererSupport.Point(baseX + (index * (participantWidth + svgOptions.HorizontalSpacing)), baseY)))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var messageYs = document.Edges.Select((edge, index) => new KeyValuePair<DiagramEdge, double>(edge, baseY + participantHeight + 48 + (index * svgOptions.VerticalSpacing)))
            .ToDictionary(x => x.Key, x => x.Value);
        var noteRects = CreateNoteRects(document.Notes, participantCenters, baseY + participantHeight + 48 + (document.Edges.Count * svgOptions.VerticalSpacing), svgOptions);
        var canvas = CalculateCanvas(participantCenters, noteRects.Values, messageYs.Values.DefaultIfEmpty(baseY + participantHeight + 48).Max(), participantWidth, participantHeight, svgOptions);
        var outputWidth = svgOptions.Width ?? canvas.Width;
        var outputHeight = svgOptions.Height ?? canvas.Height;

        var builder = new StringBuilder();
        SvgDiagramRendererSupport.AppendDocumentStart(
            builder,
            svgOptions,
            outputWidth,
            outputHeight,
            "Sequence diagram",
            [SvgDiagramRendererSupport.CreateArrowMarker(ArrowMarkerId, svgOptions.StrokeColor)]);

        foreach (var participantId in participantIds)
        {
            RenderParticipant(builder, participantMap.GetValueOrDefault(participantId) ?? new DiagramNode(participantId), participantCenters[participantId], outputHeight - svgOptions.Margin, svgOptions);
        }

        foreach (var edge in document.Edges)
        {
            RenderEdge(builder, edge, participantCenters, messageYs[edge], participantWidth, svgOptions);
        }

        foreach (var note in document.Notes)
        {
            if (noteRects.TryGetValue(note, out var rect) && participantCenters.TryGetValue(note.TargetId, out var participantCenter))
            {
                RenderNote(builder, note, rect, participantCenter, svgOptions);
            }
        }

        SvgDiagramRendererSupport.AppendDocumentEnd(builder);
        return DiagramRenderResult.FromText(DiagramRenderFormat.Svg, builder.ToString(), SvgDiagramRendererSupport.ContentType);
    }

    private static List<string> GetOrderedParticipantIds(DiagramDocument document)
    {
        return document.Nodes.Select(node => node.Id)
            .Concat(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Concat(document.Notes.Select(note => note.TargetId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<DiagramNote, SvgDiagramRendererSupport.Rect> CreateNoteRects(
        IReadOnlyList<DiagramNote> notes,
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> participantCenters,
        double baseY,
        SvgDiagramRenderOptions options)
    {
        var result = new Dictionary<DiagramNote, SvgDiagramRendererSupport.Rect>();
        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            if (!participantCenters.TryGetValue(note.TargetId, out var center))
            {
                continue;
            }

            var noteY = baseY + (i * options.VerticalSpacing);
            var noteCenter = note.Position switch
            {
                DiagramNotePosition.Left => new SvgDiagramRendererSupport.Point(center.X - (options.NodeWidth / 2d) - options.HorizontalSpacing / 2d, noteY),
                DiagramNotePosition.Above => new SvgDiagramRendererSupport.Point(center.X, noteY - options.NoteHeight / 2d),
                DiagramNotePosition.Below => new SvgDiagramRendererSupport.Point(center.X, noteY + options.NoteHeight / 2d),
                _ => new SvgDiagramRendererSupport.Point(center.X + (options.NodeWidth / 2d) + options.HorizontalSpacing / 2d, noteY),
            };

            result[note] = new SvgDiagramRendererSupport.Rect(noteCenter.X - (options.NoteWidth / 2d), noteCenter.Y - (options.NoteHeight / 2d), options.NoteWidth, options.NoteHeight);
        }

        return result;
    }

    private static SvgDiagramRendererSupport.Size CalculateCanvas(
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> participantCenters,
        IEnumerable<SvgDiagramRendererSupport.Rect> noteRects,
        double lastMessageY,
        double participantWidth,
        double participantHeight,
        SvgDiagramRenderOptions options)
    {
        if (participantCenters.Count == 0)
        {
            return new SvgDiagramRendererSupport.Size(options.Margin * 2 + participantWidth, options.Margin * 2 + participantHeight);
        }

        var left = participantCenters.Values.Min(point => point.X) - (participantWidth / 2d) - options.Margin;
        var right = participantCenters.Values.Max(point => point.X) + (participantWidth / 2d) + options.Margin;
        var bottom = lastMessageY + options.NoteHeight + options.Margin + 24;

        foreach (var rect in noteRects)
        {
            left = Math.Min(left, rect.Left - options.Margin / 2d);
            right = Math.Max(right, rect.Right + options.Margin / 2d);
            bottom = Math.Max(bottom, rect.Bottom + options.Margin / 2d);
        }

        return new SvgDiagramRendererSupport.Size(right - Math.Min(left, 0), bottom);
    }

    private static void RenderParticipant(
        StringBuilder builder,
        DiagramNode participant,
        SvgDiagramRendererSupport.Point center,
        double lifelineBottom,
        SvgDiagramRenderOptions options)
    {
        var x = center.X - (options.NodeWidth / 2d);
        var y = center.Y - (options.NodeHeight / 2d);
        builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(x)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(y)}\" width=\"{options.NodeWidth}\" height=\"{options.NodeHeight}\" rx=\"{options.CornerRadius}\" ry=\"{options.CornerRadius}\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.FillColor)}\" />");
        if (participant.Kind == DiagramNodeKind.Actor)
        {
            var iconX = x + 18;
            var iconY = center.Y - 8;
            builder.Append($"<circle cx=\"{SvgDiagramRendererSupport.FormatNumber(iconX)}\" cy=\"{SvgDiagramRendererSupport.FormatNumber(iconY - 8)}\" r=\"6\" fill=\"none\" />");
            builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(iconX)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(iconY - 2)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(iconX)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 12)}\" />");
            builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(iconX - 8)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 2)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(iconX + 8)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 2)}\" />");
            builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(iconX)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 12)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(iconX - 8)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 22)}\" />");
            builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(iconX)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 12)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(iconX + 8)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(iconY + 22)}\" />");
            SvgDiagramRendererSupport.RenderText(builder, participant.Label ?? participant.Id, center.X + 12, center.Y + 5, options);
        }
        else
        {
            SvgDiagramRendererSupport.RenderText(builder, participant.Label ?? participant.Id, center.X, center.Y + 5, options);
        }

        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(center.X)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(y + options.NodeHeight)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(center.X)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(lifelineBottom)}\" stroke-dasharray=\"6 6\" />");
    }

    private static void RenderEdge(
        StringBuilder builder,
        DiagramEdge edge,
        IReadOnlyDictionary<string, SvgDiagramRendererSupport.Point> participantCenters,
        double y,
        double participantWidth,
        SvgDiagramRenderOptions options)
    {
        if (!participantCenters.TryGetValue(edge.From, out var fromCenter) || !participantCenters.TryGetValue(edge.To, out var toCenter))
        {
            return;
        }

        var fromX = fromCenter.X + (edge.From == edge.To ? participantWidth / 3d : participantWidth / 2d);
        var toX = edge.From == edge.To ? fromCenter.X + participantWidth / 3d : toCenter.X - (participantWidth / 2d);
        var dashArray = edge.Kind == DiagramEdgeKind.Reply ? " stroke-dasharray=\"6 4\"" : string.Empty;

        if (edge.From == edge.To)
        {
            builder.Append($"<path d=\"M {SvgDiagramRendererSupport.FormatNumber(fromX)} {SvgDiagramRendererSupport.FormatNumber(y)} C {SvgDiagramRendererSupport.FormatNumber(fromX + 32)} {SvgDiagramRendererSupport.FormatNumber(y)}, {SvgDiagramRendererSupport.FormatNumber(fromX + 32)} {SvgDiagramRendererSupport.FormatNumber(y + 28)}, {SvgDiagramRendererSupport.FormatNumber(fromX)} {SvgDiagramRendererSupport.FormatNumber(y + 28)}\" fill=\"none\" marker-end=\"url(#{ArrowMarkerId})\"{dashArray} />");
            SvgDiagramRendererSupport.RenderText(builder, edge.Label, fromX + 22, y - 8, options, "start");
            return;
        }

        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(fromX)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(y)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(toX)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(y)}\" marker-end=\"url(#{ArrowMarkerId})\"{dashArray} />");
        SvgDiagramRendererSupport.RenderText(builder, edge.Label, (fromX + toX) / 2d, y - 8, options);
    }

    private static void RenderNote(
        StringBuilder builder,
        DiagramNote note,
        SvgDiagramRendererSupport.Rect rect,
        SvgDiagramRendererSupport.Point participantCenter,
        SvgDiagramRenderOptions options)
    {
        builder.Append($"<rect x=\"{SvgDiagramRendererSupport.FormatNumber(rect.X)}\" y=\"{SvgDiagramRendererSupport.FormatNumber(rect.Y)}\" width=\"{SvgDiagramRendererSupport.FormatNumber(rect.Width)}\" height=\"{SvgDiagramRendererSupport.FormatNumber(rect.Height)}\" rx=\"8\" ry=\"8\" fill=\"{SvgDiagramRendererSupport.EscapeAttribute(options.NoteFillColor)}\" stroke-dasharray=\"4 3\" />");
        builder.Append($"<line x1=\"{SvgDiagramRendererSupport.FormatNumber(participantCenter.X)}\" y1=\"{SvgDiagramRendererSupport.FormatNumber(participantCenter.Y + options.NodeHeight / 2d)}\" x2=\"{SvgDiagramRendererSupport.FormatNumber(rect.Center.X)}\" y2=\"{SvgDiagramRendererSupport.FormatNumber(rect.Center.Y)}\" stroke-dasharray=\"4 3\" />");
        SvgDiagramRendererSupport.RenderMultilineText(builder, note.Text, rect.Center.X, rect.Center.Y - 8, options);
    }
}