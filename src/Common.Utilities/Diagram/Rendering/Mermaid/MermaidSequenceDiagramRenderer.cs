// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Renders Mermaid-compatible sequence diagram text.
/// </summary>
public class MermaidSequenceDiagramRenderer : IDiagramRenderer
{
    private const string Indent = "    ";
    private const string MermaidHeader = "sequenceDiagram";

    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != DiagramKind.Sequence)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the Mermaid sequence renderer.");
        }

        options ??= new DiagramRenderOptions();

        var aliases = BuildAliasMap(document);
        var lines = new List<string>();
        if (options.IncludeHeader)
        {
            lines.Add(MermaidHeader);
        }

        lines.AddRange(RenderParticipants(document.Nodes, document.Edges, document.Notes, aliases));
        lines.AddRange(document.Edges.Select(edge => RenderEdge(edge, aliases)));

        var notes = RenderNotes(document.Notes, aliases);
        if (notes.Count > 0)
        {
            lines.AddRange(notes);
        }

        return DiagramRenderResult.FromText(DiagramRenderFormat.Mermaid, JoinLines(lines));
    }

    private static Dictionary<string, string> BuildAliasMap(DiagramDocument document)
    {
        return document.Nodes
            .Select(node => node.Id)
            .Concat(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Concat(document.Notes.Select(note => note.TargetId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, MermaidNaming.NormalizeIdentifier, StringComparer.Ordinal);
    }

    private static List<string> RenderParticipants(
        IReadOnlyList<DiagramNode> nodes,
        IReadOnlyList<DiagramEdge> edges,
        IReadOnlyList<DiagramNote> notes,
        IReadOnlyDictionary<string, string> aliases)
    {
        var orderedIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in nodes.Select(node => node.Id)
                     .Concat(edges.SelectMany(edge => new[] { edge.From, edge.To }))
                     .Concat(notes.Select(note => note.TargetId)))
        {
            if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
            {
                orderedIds.Add(id);
            }
        }

        var nodeMap = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var id in orderedIds)
        {
            var alias = aliases[id];
            var node = nodeMap.GetValueOrDefault(id);
            var keyword = node?.Kind == DiagramNodeKind.Actor ? "actor" : "participant";
            var label = MermaidEscaping.EscapeQuotedText(node?.Label);

            lines.Add(string.IsNullOrWhiteSpace(label) || string.Equals(label, alias, StringComparison.Ordinal)
                ? $"{Indent}{keyword} {alias}"
                : $"{Indent}{keyword} {alias} as \"{label}\"");
        }

        return lines;
    }

    private static string RenderEdge(DiagramEdge edge, IReadOnlyDictionary<string, string> aliases)
    {
        var arrow = edge.Kind switch
        {
            DiagramEdgeKind.Reply => "-->>",
            _ => "->>",
        };

        var label = MermaidEscaping.EscapeText(edge.Label);
        return string.IsNullOrWhiteSpace(label)
            ? $"{Indent}{aliases[edge.From]}{arrow}{aliases[edge.To]}"
            : $"{Indent}{aliases[edge.From]}{arrow}{aliases[edge.To]}: {label}";
    }

    private static List<string> RenderNotes(IEnumerable<DiagramNote> notes, IReadOnlyDictionary<string, string> aliases)
    {
        var lines = new List<string>();

        foreach (var note in notes)
        {
            var text = MermaidEscaping.EscapeText(note.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var position = note.Position switch
            {
                DiagramNotePosition.Left => "left",
                DiagramNotePosition.Above or DiagramNotePosition.Below => "over",
                _ => "right",
            };

            lines.Add(position == "over"
                ? $"{Indent}Note over {aliases[note.TargetId]}: {text}"
                : $"{Indent}Note {position} of {aliases[note.TargetId]}: {text}");
        }

        return lines;
    }

    private static string JoinLines(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            builder.Append(lines[index]);
            if (index < lines.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }
}