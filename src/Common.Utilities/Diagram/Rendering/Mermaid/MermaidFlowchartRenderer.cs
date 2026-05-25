// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal static class MermaidFlowchartRenderer
{
    private const string Indent = "    ";

    public static string Render(DiagramDocument document, DiagramRenderOptions options, DiagramKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != expectedKind)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the Mermaid {expectedKind.ToString().ToLowerInvariant()} renderer.");
        }

        options ??= new DiagramRenderOptions();

        var aliases = BuildAliasMap(document);
        var groupMap = BuildGroupMap(document.Groups);
        var lines = new List<string>();
        if (options.IncludeHeader)
        {
            lines.Add($"flowchart {ToDirection(document.Direction)}");
        }

        var explicitNodeIds = document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var orderedNodeIds = document.Nodes.Select(node => node.Id)
            .Concat(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Concat(document.Notes.Select(note => note.TargetId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var nodeMap = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var renderedNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in document.Groups)
        {
            var groupLabel = MermaidEscaping.EscapeQuotedText(group.Label) ?? MermaidNaming.NormalizeIdentifier(group.Id);
            var groupId = MermaidNaming.NormalizeIdentifier(group.Id);
            lines.Add($"{Indent}subgraph {groupId}[\"{groupLabel}\"]");

            foreach (var nodeId in orderedNodeIds.Where(id => group.NodeIds.Contains(id, StringComparer.Ordinal)))
            {
                lines.Add($"{Indent}{Indent}{RenderNode(nodeMap.GetValueOrDefault(nodeId) ?? new DiagramNode(nodeId), aliases[nodeId], expectedKind)}");
                renderedNodeIds.Add(nodeId);
            }

            lines.Add($"{Indent}end");
        }

        foreach (var nodeId in orderedNodeIds.Where(id => !renderedNodeIds.Contains(id)))
        {
            lines.Add($"{Indent}{RenderNode(nodeMap.GetValueOrDefault(nodeId) ?? new DiagramNode(nodeId), aliases[nodeId], expectedKind)}");
        }

        if (orderedNodeIds.Count > 0 && (document.Edges.Count > 0 || document.Notes.Count > 0))
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(document.Edges.Select(edge => $"{Indent}{RenderEdge(edge, aliases)}"));

        if (document.Notes.Count > 0)
        {
            if (document.Edges.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(document.Notes.Select(note => $"{Indent}{RenderNote(note, aliases)}"));
        }

        return string.Join('\n', lines);
    }

    private static Dictionary<string, string> BuildAliasMap(DiagramDocument document)
    {
        return document.Nodes.Select(node => node.Id)
            .Concat(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Concat(document.Notes.Select(note => note.TargetId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, MermaidNaming.NormalizeIdentifier, StringComparer.Ordinal);
    }

    private static Dictionary<string, DiagramGroup> BuildGroupMap(IEnumerable<DiagramGroup> groups)
    {
        var map = new Dictionary<string, DiagramGroup>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var nodeId in group.NodeIds)
            {
                map.TryAdd(nodeId, group);
            }
        }

        return map;
    }

    private static string RenderNode(DiagramNode node, string alias, DiagramKind kind)
    {
        var label = BuildNodeLabel(node, alias, kind);
        return node.Kind switch
        {
            DiagramNodeKind.Start => $"{alias}([\"{label}\"])",
            DiagramNodeKind.Terminal => $"{alias}((\"{label}\"))",
            DiagramNodeKind.Decision or DiagramNodeKind.Branch => $"{alias}{{\"{label}\"}}",
            DiagramNodeKind.Component => $"{alias}[[\"{label}\"]]",
            DiagramNodeKind.Database => $"{alias}[(\"{label}\")]",
            _ => $"{alias}[\"{label}\"]",
        };
    }

    private static string BuildNodeLabel(DiagramNode node, string alias, DiagramKind kind)
    {
        var label = MermaidEscaping.EscapeQuotedText(node.Label) ?? alias;
        if (kind == DiagramKind.Component && !string.IsNullOrWhiteSpace(node.Stereotype))
        {
            return $"{label}<br/><<{MermaidEscaping.EscapeText(node.Stereotype)}>>";
        }

        return label;
    }

    private static string RenderEdge(DiagramEdge edge, IReadOnlyDictionary<string, string> aliases)
    {
        var from = aliases[edge.From];
        var to = aliases[edge.To];
        var label = MermaidEscaping.EscapeText(edge.Label);
        var dotted = edge.Kind is DiagramEdgeKind.Dependency or DiagramEdgeKind.Realization or DiagramEdgeKind.Reply;

        if (string.IsNullOrWhiteSpace(label))
        {
            return dotted ? $"{from} -.-> {to}" : $"{from} --> {to}";
        }

        return dotted
            ? $"{from} -. {label} .-> {to}"
            : $"{from} -->|{label}| {to}";
    }

    private static string RenderNote(DiagramNote note, IReadOnlyDictionary<string, string> aliases)
    {
        var position = note.Position switch
        {
            DiagramNotePosition.Left => "left",
            DiagramNotePosition.Above => "above",
            DiagramNotePosition.Below => "below",
            _ => "right",
        };

        return $"%% note {position} of {aliases[note.TargetId]}: {MermaidEscaping.EscapeText(note.Text)}";
    }

    private static string ToDirection(DiagramDirection direction)
    {
        return direction switch
        {
            DiagramDirection.LeftToRight => "LR",
            _ => "TD",
        };
    }
}