// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Renders Mermaid-compatible class diagram text.
/// </summary>
public class MermaidClassDiagramRenderer : IDiagramRenderer
{
    private const string Indent = "    ";
    private const string MermaidHeader = "classDiagram";

    /// <inheritdoc />
    public DiagramRenderResult Render(DiagramDocument document, DiagramRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind != DiagramKind.Class)
        {
            throw new NotSupportedException($"Diagram kind '{document.Kind}' is not supported by the Mermaid class renderer.");
        }

        options ??= new DiagramRenderOptions();

        var aliases = BuildAliasMap(document.Nodes, document.Edges);
        var lines = new List<string>();
        if (options.IncludeHeader)
        {
            lines.Add(MermaidHeader);
        }

        lines.AddRange(document.Nodes.Select(node => RenderNode(node, aliases)));
        if (document.Nodes.Count > 0 && document.Edges.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(document.Edges.Select(edge => RenderEdge(edge, aliases)));
        return DiagramRenderResult.FromText(DiagramRenderFormat.Mermaid, JoinLines(lines));
    }

    private static Dictionary<string, string> BuildAliasMap(IEnumerable<DiagramNode> nodes, IEnumerable<DiagramEdge> edges)
    {
        return nodes.Select(node => node.Id)
            .Concat(edges.SelectMany(edge => new[] { edge.From, edge.To }))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, MermaidNaming.NormalizeIdentifier, StringComparer.Ordinal);
    }

    private static string RenderNode(DiagramNode node, IReadOnlyDictionary<string, string> aliases)
    {
        var alias = aliases[node.Id];
        var members = node.Members ?? [];
        var stereotype = ResolveStereotype(node);
        var needsBlock = members.Count > 0 || !string.IsNullOrWhiteSpace(stereotype);
        var label = MermaidEscaping.EscapeQuotedText(node.Label);

        if (!needsBlock)
        {
            return string.IsNullOrWhiteSpace(label) || string.Equals(label, alias, StringComparison.Ordinal)
                ? $"{Indent}class {alias}"
                : $"{Indent}class {alias}[\"{label}\"]";
        }

        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(label) || string.Equals(label, alias, StringComparison.Ordinal)
                ? $"{Indent}class {alias} {{"
                : $"{Indent}class {alias}[\"{label}\"] {{",
        };

        if (!string.IsNullOrWhiteSpace(stereotype))
        {
            lines.Add($"{Indent}{Indent}<<{stereotype}>>");
        }

        lines.AddRange(members.Select(member => $"{Indent}{Indent}{RenderMember(member)}"));
        lines.Add($"{Indent}}}");
        return string.Join('\n', lines);
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

        var name = MermaidEscaping.EscapeText(member.Name);
        var type = MermaidEscaping.EscapeText(member.Type);

        return member.Kind switch
        {
            DiagramMemberKind.Method => string.IsNullOrWhiteSpace(type)
                ? $"{prefix}{EnsureMethodSignature(name)}"
                : $"{prefix}{EnsureMethodSignature(name)}: {type}",
            _ => string.IsNullOrWhiteSpace(type)
                ? $"{prefix}{name}"
                : $"{prefix}{name}: {type}",
        };
    }

    private static string RenderEdge(DiagramEdge edge, IReadOnlyDictionary<string, string> aliases)
    {
        var (left, arrow, right) = edge.Kind switch
        {
            DiagramEdgeKind.Inheritance => (aliases[edge.To], "<|--", aliases[edge.From]),
            DiagramEdgeKind.Realization => (aliases[edge.To], "<|..", aliases[edge.From]),
            DiagramEdgeKind.Composition => (aliases[edge.From], "*--", aliases[edge.To]),
            DiagramEdgeKind.Aggregation => (aliases[edge.From], "o--", aliases[edge.To]),
            DiagramEdgeKind.Dependency => (aliases[edge.From], "..>", aliases[edge.To]),
            _ => (aliases[edge.From], "-->", aliases[edge.To]),
        };

        var label = MermaidEscaping.EscapeText(edge.Label);
        return string.IsNullOrWhiteSpace(label)
            ? $"{Indent}{left} {arrow} {right}"
            : $"{Indent}{left} {arrow} {right} : {label}";
    }

    private static string ResolveStereotype(DiagramNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Stereotype))
        {
            return MermaidEscaping.EscapeText(node.Stereotype);
        }

        return node.Kind switch
        {
            DiagramNodeKind.Interface => "interface",
            DiagramNodeKind.AbstractClass => "abstract",
            DiagramNodeKind.Enum => "enumeration",
            _ => null,
        };
    }

    private static string EnsureMethodSignature(string name)
    {
        return name.Contains('(', StringComparison.Ordinal) ? name : $"{name}()";
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