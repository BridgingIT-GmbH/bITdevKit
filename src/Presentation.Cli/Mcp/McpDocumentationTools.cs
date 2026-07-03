namespace BridgingIT.DevKit.Cli;

using System.Net.Http;
using System.Net.Http.Headers;
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
                .Take(limit)
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
                results.Length == 0 ? $"No DevKit documentation matched '{query}'." : $"Found {results.Length} DevKit documentation result(s) for '{query}'.",
                new
                {
                    query,
                    source = source.Name,
                    results
                },
                truncated: results.Length == limit);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.DocumentationUnavailable,
                "DevKit documentation search is unavailable.",
                exception.Message,
                [new McpNextCall("bdk_mcp_explain_setup", new { })]);
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
            var content = document.Content ?? string.Empty;
            var truncated = content.Length > maxChars;
            if (truncated)
            {
                content = content[..maxChars];
            }

            return McpResponse.Success(
                $"Loaded DevKit documentation source '{document.Source}'.",
                new
                {
                    source = document.Source,
                    url = document.Url,
                    content
                },
                truncated);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.DocumentationUnavailable,
                "DevKit documentation content is unavailable.",
                exception.Message,
                [new McpNextCall("bdk_docs_search", new { query = sourceId })]);
        }
    }

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

/// <summary>
/// Reads official DevKit documentation from GitHub.
/// </summary>
/// <example>
/// <code>
/// services.AddSingleton&lt;IMcpDocumentationSource, GitHubMcpDocumentationSource&gt;();
/// </code>
/// </example>
public sealed class GitHubMcpDocumentationSource(HttpClient httpClient) : IMcpDocumentationSource
{
    private const string Owner = "bridgingit";
    private const string Repository = "bitdevkit";
    private const string Branch = "main";
    private const string ContentsApiRoot = "https://api.github.com/repos/" + Owner + "/" + Repository + "/contents/docs?ref=" + Branch;
    private const string WebRoot = "https://github.com/" + Owner + "/" + Repository + "/blob/" + Branch + "/";
    private const string RawRoot = "https://raw.githubusercontent.com/" + Owner + "/" + Repository + "/" + Branch + "/";
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private IReadOnlyList<McpDocumentationDocument> cache;
    private DateTimeOffset cacheCreatedAt;

    /// <inheritdoc />
    public string Name => "official GitHub documentation";

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpDocumentationDocument>> ListAsync(CancellationToken cancellationToken)
    {
        await this.cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.cache is not null && DateTimeOffset.UtcNow - this.cacheCreatedAt < TimeSpan.FromMinutes(10))
            {
                return this.cache;
            }

            var documents = await this.ReadDocumentsAsync(cancellationToken).ConfigureAwait(false);
            this.cache = documents;
            this.cacheCreatedAt = DateTimeOffset.UtcNow;

            return documents;
        }
        finally
        {
            this.cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<McpDocumentationDocument> GetAsync(string source, CancellationToken cancellationToken)
    {
        var normalized = NormalizeSource(source);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var documents = await this.ListAsync(cancellationToken).ConfigureAwait(false);
        var found = documents.FirstOrDefault(document =>
            string.Equals(document.Source, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(document.Url, source, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
        {
            return found;
        }

        if (!normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) || !normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var content = await this.GetStringAsync(RawRoot + normalized, cancellationToken).ConfigureAwait(false);
        return new McpDocumentationDocument(normalized, WebRoot + normalized, content);
    }

    private async Task<IReadOnlyList<McpDocumentationDocument>> ReadDocumentsAsync(CancellationToken cancellationToken)
    {
        var files = new List<GitHubContentItem>();
        await this.CollectMarkdownFilesAsync(ContentsApiRoot, files, cancellationToken).ConfigureAwait(false);

        var documents = new List<McpDocumentationDocument>(files.Count);
        foreach (var file in files.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            var downloadUrl = string.IsNullOrWhiteSpace(file.DownloadUrl) ? RawRoot + file.Path : file.DownloadUrl;
            var content = await this.GetStringAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
            documents.Add(new McpDocumentationDocument(file.Path, string.IsNullOrWhiteSpace(file.HtmlUrl) ? WebRoot + file.Path : file.HtmlUrl, content));
        }

        return documents;
    }

    private async Task CollectMarkdownFilesAsync(string url, ICollection<GitHubContentItem> files, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await this.GetStringAsync(url, cancellationToken).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var type = GetString(item, "type");
            var path = GetString(item, "path");
            if (string.Equals(type, "file", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(new GitHubContentItem(path, GetString(item, "url"), GetString(item, "download_url"), GetString(item, "html_url")));
            }
            else if (string.Equals(type, "dir", StringComparison.OrdinalIgnoreCase))
            {
                var childUrl = GetString(item, "url");
                if (!string.IsNullOrWhiteSpace(childUrl))
                {
                    await this.CollectMarkdownFilesAsync(childUrl, files, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("bdk-mcp", typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeSource(string source)
    {
        var value = source.Replace('\\', '/').Trim();
        if (value.StartsWith(WebRoot, StringComparison.OrdinalIgnoreCase))
        {
            value = value[WebRoot.Length..];
        }
        else if (value.StartsWith(RawRoot, StringComparison.OrdinalIgnoreCase))
        {
            value = value[RawRoot.Length..];
        }
        else if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return null;
        }

        return value.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ? value : null;
    }

    private static string GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    private sealed record GitHubContentItem(string Path, string ApiUrl, string DownloadUrl, string HtmlUrl);
}
