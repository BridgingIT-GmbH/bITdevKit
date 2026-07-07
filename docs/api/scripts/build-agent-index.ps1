[CmdletBinding()]
param(
    [string] $MetadataRoot,
    [string] $OutputRoot,
    [string] $SiteUrl = 'https://bridgingit-gmbh.github.io/bITdevKit/api/'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($MetadataRoot)) {
    $MetadataRoot = Join-Path $repoRoot 'docs\api\obj\api'
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot '.github\pages\api'
}

if (-not ('AgentApiReferenceIndexBuilder' -as [type])) {
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class AgentApiReferenceIndexBuilder
{
    private static readonly Regex XrefRegex = new("<xref[^>]*href=\"([^\"]+)\"[^>]*></xref>", RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static int Build(string metadataRoot, string outputRoot, string siteUrl)
    {
        if (!Directory.Exists(metadataRoot))
        {
            throw new DirectoryNotFoundException("DocFX metadata root '" + metadataRoot + "' does not exist.");
        }

        Directory.CreateDirectory(outputRoot);
        var symbolRoot = Path.Combine(outputRoot, "agent-symbols");
        Directory.CreateDirectory(symbolRoot);

        foreach (var oldFile in Directory.EnumerateFiles(symbolRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            File.Delete(oldFile);
        }

        var siteUrlValue = siteUrl.EndsWith("/", StringComparison.Ordinal) ? siteUrl : siteUrl + "/";
        var indexSymbols = new List<Dictionary<string, object>>();

        foreach (var file in Directory.EnumerateFiles(metadataRoot, "*.yml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var raw = File.ReadAllText(file);
            if (!raw.Contains("YamlMime:ManagedReference", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var starts = new List<int>();
            var inItems = false;
            for (var i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i], "items:", StringComparison.Ordinal))
                {
                    inItems = true;
                    continue;
                }

                if (string.Equals(lines[i], "references:", StringComparison.Ordinal))
                {
                    break;
                }

                if (inItems && IsItemStart(lines[i]))
                {
                    starts.Add(i);
                }
            }

            if (starts.Count == 0)
            {
                continue;
            }

            var fileBaseName = Path.GetFileNameWithoutExtension(file);
            var detailName = StableHash(fileBaseName) + ".json";
            var detailPath = "agent-symbols/" + detailName;
            var pageDetails = new List<Dictionary<string, object>>();

            for (var i = 0; i < starts.Count; i++)
            {
                var start = starts[i];
                var end = i + 1 < starts.Count ? starts[i + 1] : lines.Length;
                var item = ConvertItem(lines, start, end, fileBaseName, detailPath, siteUrlValue);
                if (item == null)
                {
                    continue;
                }

                indexSymbols.Add(item.Index);
                pageDetails.Add(item.Detail);
            }

            if (pageDetails.Count > 0)
            {
                var page = new Dictionary<string, object>
                {
                    ["schemaVersion"] = 1,
                    ["source"] = "docfx-mref-page",
                    ["file"] = Path.GetFileName(file),
                    ["symbols"] = pageDetails
                };
                File.WriteAllText(Path.Combine(symbolRoot, detailName), JsonSerializer.Serialize(page, JsonOptions), Encoding.UTF8);
            }
        }

        var orderedSymbols = indexSymbols
            .OrderBy(s => Value(s, "fullName"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => Value(s, "uid"), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var index = new Dictionary<string, object>
        {
            ["schemaVersion"] = 1,
            ["generatedAt"] = DateTimeOffset.UtcNow.ToString("o"),
            ["siteUrl"] = siteUrlValue,
            ["source"] = "docfx-mref",
            ["symbols"] = orderedSymbols
        };

        File.WriteAllText(Path.Combine(outputRoot, "agent-index.json"), JsonSerializer.Serialize(index, JsonOptions), Encoding.UTF8);
        return orderedSymbols.Count;
    }

    private static ItemPair ConvertItem(string[] lines, int start, int end, string fileBaseName, string detailPath, string siteUrl)
    {
        var uid = lines[start].Substring(7).Trim();
        if (string.IsNullOrWhiteSpace(uid) || !uid.StartsWith("BridgingIT.DevKit.", StringComparison.Ordinal))
        {
            return null;
        }

        string name = null;
        string fullName = null;
        string kind = null;
        string ns = null;
        string assembly = null;
        string summary = null;
        string remarks = null;
        string href = null;
        string parent = null;
        var children = new List<string>();
        var extensionMethods = new List<string>();
        var examples = new List<string>();
        Dictionary<string, object> syntax = null;
        List<Dictionary<string, object>> parameters = new();
        Dictionary<string, object> returns = null;
        var isExternal = false;

        for (var cursor = start + 1; cursor < end; cursor++)
        {
            var line = lines[cursor];
            if (!IsTopField(line))
            {
                continue;
            }

            var separator = line.IndexOf(':', 2);
            var field = line.Substring(2, separator - 2);
            var inline = line.Substring(separator + 1).Trim();

            switch (field)
            {
                case "name":
                    name = ReadScalar(lines, ref cursor, end, inline, 2, 500);
                    break;
                case "fullName":
                    fullName = ReadScalar(lines, ref cursor, end, inline, 2, 1000);
                    break;
                case "type":
                    kind = ReadScalar(lines, ref cursor, end, inline, 2, 100);
                    break;
                case "namespace":
                    ns = ReadScalar(lines, ref cursor, end, inline, 2, 500);
                    break;
                case "summary":
                    summary = ReadScalar(lines, ref cursor, end, inline, 2, 1200);
                    break;
                case "remarks":
                    remarks = ReadScalar(lines, ref cursor, end, inline, 2, 4000);
                    break;
                case "href":
                    href = ReadScalar(lines, ref cursor, end, inline, 2, 1000);
                    break;
                case "parent":
                    parent = ReadScalar(lines, ref cursor, end, inline, 2, 1000);
                    break;
                case "isExternal":
                    isExternal = string.Equals(ReadScalar(lines, ref cursor, end, inline, 2, 20), "true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "assemblies":
                    var assemblies = ReadList(lines, ref cursor, end, 10);
                    assembly = assemblies.Count > 0 ? assemblies[0] : null;
                    break;
                case "children":
                    children = ReadList(lines, ref cursor, end, 500);
                    break;
                case "extensionMethods":
                    extensionMethods = ReadList(lines, ref cursor, end, 500);
                    break;
                case "example":
                    var example = ReadScalar(lines, ref cursor, end, inline, 2, 3000);
                    if (!string.IsNullOrWhiteSpace(example) && example != "[]")
                    {
                        examples.Add(example);
                    }
                    break;
                case "syntax":
                    syntax = ReadSyntax(lines, ref cursor, end);
                    parameters = syntax.TryGetValue("parameters", out var parameterValue) && parameterValue is List<Dictionary<string, object>> parameterList ? parameterList : new List<Dictionary<string, object>>();
                    returns = syntax.TryGetValue("returns", out var returnValue) && returnValue is Dictionary<string, object> returnDictionary ? returnDictionary : null;
                    break;
            }
        }

        if (isExternal || string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(href))
        {
            href = RelativeHref(fileBaseName, uid, kind);
        }

        var topics = Topics(uid, name, fullName, ns, assembly);
        var index = new Dictionary<string, object>
        {
            ["uid"] = uid,
            ["name"] = name,
            ["fullName"] = fullName,
            ["kind"] = kind,
            ["namespace"] = ns,
            ["assembly"] = assembly,
            ["summary"] = summary,
            ["href"] = href,
            ["detail"] = detailPath,
            ["topics"] = topics
        };

        var detail = new Dictionary<string, object>
        {
            ["uid"] = uid,
            ["name"] = name,
            ["fullName"] = fullName,
            ["kind"] = kind,
            ["namespace"] = ns,
            ["assembly"] = assembly,
            ["summary"] = summary,
            ["remarks"] = remarks,
            ["syntax"] = syntax,
            ["parameters"] = parameters,
            ["returns"] = returns,
            ["parent"] = parent,
            ["children"] = children.Take(100).ToList(),
            ["extensionMethods"] = extensionMethods.Take(100).ToList(),
            ["examples"] = examples,
            ["url"] = siteUrl + href,
            ["topics"] = topics
        };

        return new ItemPair(index, detail);
    }

    private static Dictionary<string, object> ReadSyntax(string[] lines, ref int index, int end)
    {
        string content = null;
        var parameters = new List<Dictionary<string, object>>();
        Dictionary<string, object> returns = null;
        var cursor = index + 1;

        while (cursor < end)
        {
            var line = lines[cursor];
            if (IsItemStart(line) || IsTopField(line))
            {
                break;
            }

            if (line.StartsWith("    content:", StringComparison.Ordinal))
            {
                content = ReadNestedScalar(lines, ref cursor, end, 4, "    content:", 4000);
            }
            else if (line.StartsWith("    parameters:", StringComparison.Ordinal))
            {
                parameters = ReadParameters(lines, ref cursor, end);
            }
            else if (line.StartsWith("    return:", StringComparison.Ordinal))
            {
                returns = ReadReturn(lines, ref cursor, end);
            }

            cursor++;
        }

        index = cursor - 1;
        return new Dictionary<string, object>
        {
            ["content"] = content,
            ["parameters"] = parameters,
            ["returns"] = returns
        };
    }

    private static List<Dictionary<string, object>> ReadParameters(string[] lines, ref int index, int end)
    {
        var parameters = new List<Dictionary<string, object>>();
        var cursor = index + 1;

        while (cursor < end)
        {
            var line = lines[cursor];
            if (IsItemStart(line) || IsTopField(line))
            {
                break;
            }

            var indent = Indent(line);
            if (indent <= 4 && line.TrimEnd().EndsWith(":", StringComparison.Ordinal) && !line.StartsWith("    - id:", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("    - id:", StringComparison.Ordinal))
            {
                var id = Clean(line.Substring(9), 200);
                string type = null;
                string description = null;
                cursor++;

                while (cursor < end)
                {
                    var field = lines[cursor];
                    if (IsItemStart(field) ||
                        IsTopField(field) ||
                        field.StartsWith("    - id:", StringComparison.Ordinal) ||
                        field.StartsWith("    return:", StringComparison.Ordinal) ||
                        field.StartsWith("    typeParameters:", StringComparison.Ordinal))
                    {
                        cursor--;
                        break;
                    }

                    if (field.StartsWith("      type:", StringComparison.Ordinal))
                    {
                        type = ReadNestedScalar(lines, ref cursor, end, 6, "      type:", 500);
                    }
                    else if (field.StartsWith("      description:", StringComparison.Ordinal))
                    {
                        description = ReadNestedScalar(lines, ref cursor, end, 6, "      description:", 1000);
                    }

                    cursor++;
                }

                parameters.Add(new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["type"] = type,
                    ["description"] = description
                });
            }

            cursor++;
        }

        index = cursor - 1;
        return parameters;
    }

    private static Dictionary<string, object> ReadReturn(string[] lines, ref int index, int end)
    {
        string type = null;
        string description = null;
        var cursor = index + 1;

        while (cursor < end)
        {
            var line = lines[cursor];
            if (IsItemStart(line) || IsTopField(line))
            {
                break;
            }

            if (Indent(line) <= 4 && line.TrimEnd().EndsWith(":", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("      type:", StringComparison.Ordinal))
            {
                type = ReadNestedScalar(lines, ref cursor, end, 6, "      type:", 500);
            }
            else if (line.StartsWith("      description:", StringComparison.Ordinal))
            {
                description = ReadNestedScalar(lines, ref cursor, end, 6, "      description:", 1000);
            }

            cursor++;
        }

        index = cursor - 1;
        return string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(description)
            ? null
            : new Dictionary<string, object> { ["type"] = type, ["description"] = description };
    }

    private static string ReadScalar(string[] lines, ref int index, int end, string inline, int fieldIndent, int maxLength)
    {
        var value = inline.Trim();
        if (!IsBlockScalar(value))
        {
            return Clean(value, maxLength);
        }

        var parts = new List<string>();
        var cursor = index + 1;
        while (cursor < end)
        {
            var line = lines[cursor];
            if (IsItemStart(line) || IsTopField(line))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                parts.Add("");
            }
            else if (Indent(line) > fieldIndent)
            {
                parts.Add(line.Trim());
            }
            else
            {
                break;
            }

            cursor++;
        }

        index = cursor - 1;
        return Clean(string.Join(" ", parts), maxLength);
    }

    private static string ReadNestedScalar(string[] lines, ref int index, int end, int fieldIndent, string prefix, int maxLength)
    {
        var inline = lines[index].Substring(prefix.Length).Trim();
        if (!IsBlockScalar(inline))
        {
            return Clean(inline, maxLength);
        }

        var parts = new List<string>();
        var cursor = index + 1;
        while (cursor < end)
        {
            var line = lines[cursor];
            if (IsItemStart(line) || IsTopField(line))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                parts.Add("");
            }
            else if (Indent(line) > fieldIndent)
            {
                parts.Add(line.Trim());
            }
            else
            {
                break;
            }

            cursor++;
        }

        index = cursor - 1;
        return Clean(string.Join(" ", parts), maxLength);
    }

    private static List<string> ReadList(string[] lines, ref int index, int end, int maxItems)
    {
        var values = new List<string>();
        var cursor = index + 1;

        while (cursor < end)
        {
            var line = lines[cursor];
            if (IsItemStart(line) || IsTopField(line))
            {
                break;
            }

            if (line.StartsWith("  - ", StringComparison.Ordinal))
            {
                var value = Clean(line.Substring(4), 1000);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            if (values.Count >= maxItems)
            {
                break;
            }

            cursor++;
        }

        index = cursor - 1;
        return values;
    }

    private static string Clean(string value, int maxLength)
    {
        if (value == null)
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length >= 2 && ((text[0] == '\'' && text[text.Length - 1] == '\'') || (text[0] == '"' && text[text.Length - 1] == '"')))
        {
            text = text.Substring(1, text.Length - 2);
        }

        if (text.IndexOf('<') >= 0)
        {
            text = XrefRegex.Replace(text, "$1");
            text = TagRegex.Replace(text, "");
        }

        if (text.IndexOf('\n') >= 0 || text.IndexOf('\t') >= 0 || text.IndexOf("  ", StringComparison.Ordinal) >= 0)
        {
            text = WhitespaceRegex.Replace(text, " ").Trim();
        }

        return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
    }

    private static List<string> Topics(params string[] values)
    {
        var value = string.Join(" ", values.Where(v => !string.IsNullOrWhiteSpace(v))).ToLowerInvariant();
        var topics = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (value.Contains("result")) topics.Add("results");
        if (value.Contains("rule")) topics.Add("rules");
        if (value.Contains("domain")) topics.Add("domain");
        if (value.Contains("domainevent") || value.Contains("domain.event") || value.Contains("domain_events")) topics.Add("domain_events");
        if (value.Contains("repository")) topics.Add("repositories");
        if (value.Contains("specification")) topics.Add("specifications");
        if (value.Contains("command") || value.Contains("query") || value.Contains("handler")) topics.Add("commands_queries");
        if (value.Contains("requester") || value.Contains("notifier") || value.Contains("notification")) topics.Add("requester_notifier");
        if (value.Contains("applicationevent") || value.Contains("application.event")) topics.Add("application_events");
        if (value.Contains("job") || value.Contains("jobscheduling") || value.Contains("scheduler")) topics.Add("jobs");
        if (value.Contains("message") || value.Contains("messaging") || value.Contains("broker")) topics.Add("messaging");
        if (value.Contains("queue") || value.Contains("queueing")) topics.Add("queueing");
        if (value.Contains("orchestration") || value.Contains("orchestrator") || value.Contains("workflow")) topics.Add("orchestration");
        if (value.Contains("pipeline")) topics.Add("pipelines");
        if (value.Contains("cache") || value.Contains("caching")) topics.Add("caching");
        if (value.Contains("map") || value.Contains("mapping")) topics.Add("mapping");
        if (value.Contains("serializ") || value.Contains("json")) topics.Add("serialization");
        if (value.Contains("utility") || value.Contains("utilities") || value.Contains("clock") || value.Contains("timeprovider") || value.Contains("hash") || value.Contains("clone")) topics.Add("utilities");
        if (value.Contains("filter")) topics.Add("filtering");
        if (value.Contains("module")) topics.Add("modules");
        if (value.Contains("startuptask") || value.Contains("startup")) topics.Add("startuptasks");
        if (value.Contains("documentstorage") || value.Contains("document.storage") || value.Contains("document store")) topics.Add("document_storage");
        if (value.Contains("filestorage") || value.Contains("file.storage") || value.Contains("file store")) topics.Add("file_storage");
        if (value.Contains("monitor") || value.Contains("watcher")) topics.Add("monitoring");
        if (value.Contains("dashboard")) topics.Add("dashboard");
        if (value.Contains("presentation.web")) topics.Add("presentation");
        if (value.Contains("common")) topics.Add("common");
        if (value.Contains("application")) topics.Add("application");
        if (value.Contains("infrastructure")) topics.Add("infrastructure");

        return topics.ToList();
    }

    private static bool IsBlockScalar(string value)
        => value == ">-" || value == ">" || value == "|" || value == "|-";

    private static bool IsItemStart(string line)
        => line.StartsWith("- uid: ", StringComparison.Ordinal);

    private static bool IsTopField(string line)
        => line.Length > 2 &&
            line[0] == ' ' &&
            line[1] == ' ' &&
            (line.Length < 4 || line[2] != ' ' || line[3] != ' ') &&
            !line.StartsWith("  - ", StringComparison.Ordinal) &&
            line.IndexOf(':', 2) > 2;

    private static int Indent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static string RelativeHref(string fileBaseName, string uid, string kind)
    {
        var href = "obj/api/" + fileBaseName + ".html";
        return kind == "Method" || kind == "Property" || kind == "Field" || kind == "Constructor" || kind == "Event" || kind == "Operator"
            ? href + "#" + Uri.EscapeDataString(uid)
            : href;
    }

    private static string StableHash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    private static string Value(Dictionary<string, object> values, string key)
        => values.TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;

    private sealed class ItemPair
    {
        public ItemPair(Dictionary<string, object> index, Dictionary<string, object> detail)
        {
            Index = index;
            Detail = detail;
        }

        public Dictionary<string, object> Index { get; }

        public Dictionary<string, object> Detail { get; }
    }
}
'@
}

$count = [AgentApiReferenceIndexBuilder]::Build($MetadataRoot, $OutputRoot, $SiteUrl)
Write-Host "Generated $count API reference symbol index entries in $OutputRoot"
