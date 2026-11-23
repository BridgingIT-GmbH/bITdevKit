namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;

/// <summary>
/// Implements bounded DevKit documentation lookup tools for MCP.
/// </summary>
/// <example>
/// <code>
/// var result = await docs.SearchAsync(arguments, CancellationToken.None);
/// </code>
/// </example>
public sealed class McpDocumentationTools(IMcpDocumentationSource source)
{
    private const int DefaultSearchLimit = 5;
    private const int MaxSearchLimit = 20;
    private const int DefaultMaxChars = 6000;
    private const int MaxChars = 20000;

    /// <summary>
    /// Searches official DevKit documentation and returns source links.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<McpResponse> SearchAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = McpJson.GetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "query is required.",
                "Call bdk_docs_search with a short DevKit documentation query.",
                [new McpNextCall("bdk_docs_search", new { query = "mcp diagnostics" })]);
        }

        try
        {
            var limit = McpJson.GetInt32(arguments, "limit", DefaultSearchLimit, 1, MaxSearchLimit);
            var terms = Tokenize(query);
            var documents = await source.ListAsync(cancellationToken).ConfigureAwait(false);
            var results = documents
                .Select(document => Score(document, terms))
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Source, StringComparer.OrdinalIgnoreCase)
                .Take(limit + 1)
                .Select(result => new
                {
                    source = result.Source,
                    url = result.Url,
                    score = result.Score,
                    title = result.Title,
                    excerpt = result.Excerpt
                })
                .ToArray();

            return McpResponse.Success(
                results.Length == 0 ? $"No DevKit documentation matched '{query}'." : $"Found {Math.Min(results.Length, limit)} DevKit documentation result(s) for '{query}'.",
                new
                {
                    query,
                    source = source.Name,
                    results = results.Take(limit).ToArray()
                },
                truncated: results.Length > limit);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.DocumentationUnavailable,
                "DevKit documentation search is unavailable.",
                exception.Message + " Rebuild or reinstall the local bdk tool to restore embedded documentation.",
                [new McpNextCall("bdk_mcp_status", new { })]);
        }
    }

    /// <summary>
    /// Gets bounded documentation content by source path or URL.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<McpResponse> GetAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var sourceId = McpJson.GetString(arguments, "source");
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "source is required.",
                "Call bdk_docs_search first, then pass one returned source to bdk_docs_get.",
                [new McpNextCall("bdk_docs_search", new { query = "mcp diagnostics" })]);
        }

        if (!TryReadOffset(arguments, out var offset))
        {
            return InvalidOffset();
        }

        try
        {
            var document = await source.GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return McpResponse.Unavailable(
                    McpErrorCode.DocumentationUnavailable,
                    "The documentation source was not found.",
                    sourceId,
                    [new McpNextCall("bdk_docs_search", new { query = sourceId })]);
            }

            var maxChars = McpJson.GetInt32(arguments, "maxChars", DefaultMaxChars, 1, MaxChars);
            var fullContent = document.Content ?? string.Empty;
            if (offset > fullContent.Length || SplitsSurrogatePair(fullContent, offset))
            {
                return InvalidOffset();
            }

            var length = Math.Min(maxChars, fullContent.Length - offset);
            if (SplitsSurrogatePair(fullContent, offset + length))
            {
                length--;
            }

            if (length == 0 && offset < fullContent.Length)
            {
                return McpResponse.Unavailable(McpErrorCode.OperationFailed, "maxChars must allow at least one complete Unicode character.");
            }

            var content = fullContent.Substring(offset, length);
            var truncated = offset + content.Length < fullContent.Length;
            var nextOffset = truncated ? offset + content.Length : (int?)null;

            return McpResponse.Success(
                $"Loaded DevKit documentation source '{document.Source}'.",
                new
                {
                    source = document.Source,
                    url = document.Url,
                    content,
                    offset,
                    nextOffset,
                    totalChars = fullContent.Length
                },
                truncated,
                next: nextOffset.HasValue
                    ? [new McpNextCall("bdk_docs_get", new { source = document.Source, offset = nextOffset.Value, maxChars })]
                    : []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.DocumentationUnavailable,
                "DevKit documentation content is unavailable.",
                exception.Message + " Rebuild or reinstall the local bdk tool to restore embedded documentation.",
                [new McpNextCall("bdk_mcp_status", new { })]);
        }
    }

    private static bool TryReadOffset(JsonElement arguments, out int offset)
    {
        offset = 0;
        return !arguments.TryGetProperty("offset", out var property) ||
            property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out offset) && offset >= 0;
    }

    private static McpResponse InvalidOffset()
        => McpResponse.Unavailable(
            McpErrorCode.OperationFailed,
            "offset must be a non-negative integer within the document length at a Unicode character boundary.");

    private static bool SplitsSurrogatePair(string content, int offset)
        => offset > 0 && offset < content.Length && char.IsHighSurrogate(content[offset - 1]) && char.IsLowSurrogate(content[offset]);

    private static SearchResult Score(McpDocumentationDocument document, IReadOnlyCollection<string> terms)
    {
        var content = document.Content ?? string.Empty;
        var normalized = content.ToLowerInvariant();
        var score = terms.Sum(term => Regex.Matches(normalized, Regex.Escape(term)).Count);
        var title = content.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?.TrimStart('#', ' ') ??
            Path.GetFileNameWithoutExtension(document.Source);
        var firstTerm = terms.FirstOrDefault();
        var index = string.IsNullOrWhiteSpace(firstTerm) ? -1 : normalized.IndexOf(firstTerm, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, index - 120);
        var length = Math.Min(360, content.Length - start);
        var excerpt = length > 0 ? content.Substring(start, length).ReplaceLineEndings(" ").Trim() : string.Empty;

        return new SearchResult(document.Source, document.Url, title, excerpt, score);
    }

    private static IReadOnlyList<string> Tokenize(string query)
        => Regex.Split(query.ToLowerInvariant(), @"\W+")
            .Where(term => term.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record SearchResult(string Source, string Url, string Title, string Excerpt, int Score);
}

/// <summary>
/// Provides official DevKit documentation documents to MCP documentation tools.
/// </summary>
/// <example>
/// <code>
/// var documents = await source.ListAsync(CancellationToken.None);
/// </code>
/// </example>
public interface IMcpDocumentationSource
{
    /// <summary>
    /// Gets the source display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Lists bounded searchable documentation documents.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The documents.</returns>
    Task<IReadOnlyList<McpDocumentationDocument>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a document by path or official URL.
    /// </summary>
    /// <param name="source">The source path or URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null" />.</returns>
    Task<McpDocumentationDocument> GetAsync(string source, CancellationToken cancellationToken);
}

/// <summary>
/// Represents one DevKit documentation document.
/// </summary>
/// <example>
/// <code>
/// var document = new McpDocumentationDocument("docs/index.md", "https://example/docs/index.md", "# Docs");
/// </code>
/// </example>
/// <param name="Source">The repository-relative documentation source path.</param>
/// <param name="Url">The public source URL.</param>
/// <param name="Content">The markdown content.</param>
public sealed record McpDocumentationDocument(string Source, string Url, string Content);
