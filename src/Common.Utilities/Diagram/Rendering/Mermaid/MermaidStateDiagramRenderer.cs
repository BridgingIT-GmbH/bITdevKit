// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Renders Mermaid-compatible state diagram text.
/// </summary>
public class MermaidStateDiagramRenderer : IDiagramRenderer
{
    private const string Indent = "    ";
    private const string MermaidHeader = "stateDiagram-v2";
    private const string StartOrEndIdentifier = "[*]";

    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != DiagramKind.State)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the Mermaid state renderer.");
        }

        options ??= new DiagramRenderOptions();

        var normalizedIdentifiers = BuildIdentifierMap(document);
        var lines = new List<string>();
        if (options.IncludeHeader)
        {
            lines.Add(MermaidHeader);
        }

        var explicitNodes = GetExplicitNodeDeclarations(document, normalizedIdentifiers);
        if (explicitNodes.Count > 0)
        {
            lines.AddRange(explicitNodes);
        }

        var edgeLines = document.Edges.Select(edge => RenderEdge(edge, normalizedIdentifiers)).ToList();
        if (edgeLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.AddRange(edgeLines);
            }
            else
            {
                lines = edgeLines;
            }
        }

        var noteLines = RenderNotes(document.Notes, normalizedIdentifiers);
        if (noteLines.Count > 0)
        {
            if (edgeLines.Count > 0 || explicitNodes.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(noteLines);
        }

        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            builder.Append(lines[index]);
            if (index < lines.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return DiagramRenderResult.FromText(DiagramRenderFormat.Mermaid, builder.ToString());
    }

    private static Dictionary<string, string> BuildIdentifierMap(DiagramDocument document)
    {
        var identifiers = new List<string>();
        identifiers.AddRange(document.Nodes.Select(node => node.Id));
        identifiers.AddRange(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }));
        identifiers.AddRange(document.Notes.Select(note => note.TargetId));
        identifiers.AddRange(document.Groups.SelectMany(group => group.NodeIds));

        var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var identifier in identifiers.Where(identifier => !string.IsNullOrWhiteSpace(identifier)).Distinct(StringComparer.Ordinal))
        {
            if (string.Equals(identifier, StartOrEndIdentifier, StringComparison.Ordinal))
            {
                map[identifier] = StartOrEndIdentifier;
                continue;
            }

            var baseName = MermaidNaming.NormalizeIdentifier(identifier);
            var name = baseName;
            var suffix = 2;
            while (!uniqueNames.Add(name))
            {
                name = $"{baseName}_{suffix++}";
            }

            map[identifier] = name;
        }

        return map;
    }

    private static List<string> GetExplicitNodeDeclarations(DiagramDocument document, IReadOnlyDictionary<string, string> normalizedIdentifiers)
    {
        var referencedNodeIds = new HashSet<string>(document.Edges.SelectMany(edge => new[] { edge.From, edge.To }), StringComparer.Ordinal)
        {
            StartOrEndIdentifier,
        };

        var declarations = new List<string>();
        foreach (var node in document.Nodes)
        {
            if (string.Equals(node.Id, StartOrEndIdentifier, StringComparison.Ordinal))
            {
                continue;
            }

            var normalizedId = normalizedIdentifiers[node.Id];
            var normalizedLabel = MermaidEscaping.EscapeStateLabel(node.Label);
            var requiresDeclaration = !referencedNodeIds.Contains(node.Id) || (!string.IsNullOrWhiteSpace(normalizedLabel) && !string.Equals(normalizedId, normalizedLabel, StringComparison.Ordinal));
            if (!requiresDeclaration)
            {
                continue;
            }

            declarations.Add(string.IsNullOrWhiteSpace(normalizedLabel)
                ? $"{Indent}state {normalizedId}"
                : $"{Indent}state \"{normalizedLabel}\" as {normalizedId}");
        }

        return declarations;
    }

    private static string RenderEdge(DiagramEdge edge, IReadOnlyDictionary<string, string> normalizedIdentifiers)
    {
        var from = normalizedIdentifiers[edge.From];
        var to = normalizedIdentifiers[edge.To];
        var label = MermaidEscaping.EscapeStateLabel(edge.Label);

        return string.IsNullOrWhiteSpace(label)
            ? $"{Indent}{from} --> {to}"
            : $"{Indent}{from} --> {to}: {label}";
    }

    private static List<string> RenderNotes(IEnumerable<DiagramNote> notes, IReadOnlyDictionary<string, string> normalizedIdentifiers)
    {
        var lines = new List<string>();

        foreach (var note in notes)
        {
            var noteLines = note.Text?
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(MermaidEscaping.EscapeText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (noteLines is null || noteLines.Count == 0)
            {
                continue;
            }

            var position = note.Position switch
            {
                DiagramNotePosition.Left => "left",
                _ => "right",
            };

            lines.Add($"{Indent}note {position} of {normalizedIdentifiers[note.TargetId]}");
            foreach (var text in noteLines)
            {
                lines.Add($"{Indent}{Indent}{text}");
            }

            lines.Add($"{Indent}end note");
        }

        return lines;
    }
}