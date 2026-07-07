namespace BridgingIT.DevKit.Cli;

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;

/// <summary>
/// Implements bounded DevKit API reference lookup tools for MCP.
/// </summary>
/// <example>
/// <code>
/// var result = await api.SearchAsync(arguments, CancellationToken.None);
/// </code>
/// </example>
public sealed class McpApiReferenceTools(IMcpApiReferenceSource source)
{
    private const int DefaultSearchLimit = 8;
    private const int MaxSearchLimit = 25;
    private const int DefaultMaxChars = 6000;
    private const int MaxChars = 20000;

    /// <summary>
    /// Searches the official DevKit API reference.
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
                "Call bdk_api_search with a DevKit type, member or feature keyword.",
                [new McpNextCall("bdk_api_search", new { query = "Result" })]);
        }

        try
        {
            var index = await source.GetIndexAsync(cancellationToken).ConfigureAwait(false);
            var limit = McpJson.GetInt32(arguments, "limit", DefaultSearchLimit, 1, MaxSearchLimit);
            var topic = McpJson.GetString(arguments, "topic");
            var kind = McpJson.GetString(arguments, "kind");
            var ns = McpJson.GetString(arguments, "namespace");
            var terms = Tokenize(query);

            var results = index.Symbols
                .Select(symbol => Score(symbol, query, terms, topic, kind, ns))
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Symbol.FullName, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(result => new
                {
                    result.Symbol.Uid,
                    result.Symbol.Name,
                    result.Symbol.FullName,
                    result.Symbol.Kind,
                    result.Symbol.Namespace,
                    result.Symbol.Assembly,
                    result.Symbol.Summary,
                    result.Symbol.Href,
                    result.Symbol.Detail,
                    result.Symbol.Topics,
                    url = CombineUrl(index.SiteUrl, result.Symbol.Href),
                    result.Score
                })
                .ToArray();

            return McpResponse.Success(
                results.Length == 0 ? $"No DevKit API reference symbols matched '{query}'." : $"Found {results.Length} DevKit API reference symbol(s) for '{query}'.",
                new
                {
                    query,
                    source = source.Name,
                    schemaVersion = index.SchemaVersion,
                    results
                },
                truncated: results.Length == limit,
                next: results.Length > 0
                    ? [new McpNextCall("bdk_api_get", new { uid = results[0].Uid })]
                    : [new McpNextCall("bdk_docs_search", new { query })]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.DocumentationUnavailable,
                "DevKit API reference search is unavailable.",
                exception.Message,
                [new McpNextCall("bdk_docs_search", new { query })]);
        }
    }

    /// <summary>
    /// Gets bounded API reference content by symbol uid.
    /// </summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<McpResponse> GetAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var uid = McpJson.GetString(arguments, "uid");
        if (string.IsNullOrWhiteSpace(uid))
        {
            return McpResponse.Unavailable(
                McpErrorCode.OperationFailed,
                "uid is required.",
                "Call bdk_api_search first, then pass one returned uid to bdk_api_get.",
                [new McpNextCall("bdk_api_search", new { query = "Result" })]);
        }

        try
        {
            var symbol = await source.GetSymbolAsync(uid, cancellationToken).ConfigureAwait(false);
            if (symbol is null)
            {
                return McpResponse.Unavailable(
                    McpErrorCode.DocumentationUnavailable,
                    "The API reference symbol was not found.",
                    uid,
                    [new McpNextCall("bdk_api_search", new { query = uid })]);
            }

            var maxChars = McpJson.GetInt32(arguments, "maxChars", DefaultMaxChars, 1, MaxChars);
            var content = JsonSerializer.Serialize(symbol, CliJson.Options);
            var truncated = content.Length > maxChars;
            if (truncated)
            {
                content = content[..maxChars] + "\n... truncated";
            }

            return McpResponse.Success(
                $"Loaded DevKit API reference symbol '{symbol.Uid}'.",
                new
                {
                    symbol.Uid,
                    symbol.Name,
                    symbol.FullName,
                    symbol.Kind,
                    symbol.Namespace,
                    symbol.Assembly,
                    symbol.Url,
                    content,
                    symbol = truncated ? null : symbol
                },
                truncated);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpResponse.Unavailable(
                McpErrorCode.DocumentationUnavailable,
                "DevKit API reference content is unavailable.",
                exception.Message,
                [new McpNextCall("bdk_api_search", new { query = uid })]);
        }
    }

    private static SearchResult Score(
        McpApiReferenceIndexSymbol symbol,
        string query,
        IReadOnlyCollection<string> terms,
        string topic,
        string kind,
        string ns)
    {
        if (!string.IsNullOrWhiteSpace(topic) &&
            !symbol.Topics.Any(value => string.Equals(value, topic, StringComparison.OrdinalIgnoreCase)))
        {
            return new SearchResult(symbol, 0);
        }

        if (!string.IsNullOrWhiteSpace(kind) &&
            !string.Equals(symbol.Kind, kind, StringComparison.OrdinalIgnoreCase))
        {
            return new SearchResult(symbol, 0);
        }

        if (!string.IsNullOrWhiteSpace(ns) &&
            !((symbol.Namespace ?? string.Empty).Contains(ns, StringComparison.OrdinalIgnoreCase)))
        {
            return new SearchResult(symbol, 0);
        }

        var score = 0;
        if (string.Equals(symbol.Name, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(symbol.FullName, query, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }
        else if ((symbol.Name ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
            (symbol.FullName ?? string.Empty).StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }
        else if ((symbol.Name ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (symbol.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        foreach (var term in terms)
        {
            score += Contains(symbol.Name, term) ? 25 : 0;
            score += Contains(symbol.FullName, term) ? 20 : 0;
            score += Contains(symbol.Namespace, term) ? 10 : 0;
            score += Contains(symbol.Assembly, term) ? 8 : 0;
            score += Contains(symbol.Summary, term) ? 3 : 0;
            score += symbol.Topics.Any(value => Contains(value, term)) ? 12 : 0;
        }

        return new SearchResult(symbol, score);
    }

    private static bool Contains(string value, string term)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Tokenize(string query)
        => Regex.Split(query.ToLowerInvariant(), @"\W+")
            .Where(term => term.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string CombineUrl(string root, string relative)
        => $"{(root ?? string.Empty).TrimEnd('/')}/{(relative ?? string.Empty).TrimStart('/')}";

    private sealed record SearchResult(McpApiReferenceIndexSymbol Symbol, int Score);
}

/// <summary>
/// Provides DevKit API reference symbols to MCP API reference tools.
/// </summary>
/// <example>
/// <code>
/// var index = await source.GetIndexAsync(CancellationToken.None);
/// </code>
/// </example>
public interface IMcpApiReferenceSource
{
    /// <summary>
    /// Gets the source display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the searchable API reference index.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index.</returns>
    Task<McpApiReferenceIndex> GetIndexAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a symbol by uid.
    /// </summary>
    /// <param name="uid">The DocFX symbol uid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The symbol, or <see langword="null" />.</returns>
    Task<McpApiReferenceSymbol> GetSymbolAsync(string uid, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the generated DevKit API reference index.
/// </summary>
/// <example>
/// <code>
/// var index = new McpApiReferenceIndex { SchemaVersion = 1 };
/// </code>
/// </example>
public sealed record McpApiReferenceIndex
{
    /// <summary>
    /// Gets or initializes the schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets or initializes the generation timestamp.
    /// </summary>
    public string GeneratedAt { get; init; }

    /// <summary>
    /// Gets or initializes the public API reference site URL.
    /// </summary>
    public string SiteUrl { get; init; }

    /// <summary>
    /// Gets or initializes the source format.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets or initializes searchable symbols.
    /// </summary>
    public IReadOnlyList<McpApiReferenceIndexSymbol> Symbols { get; init; } = [];
}

/// <summary>
/// Represents one searchable API reference symbol index entry.
/// </summary>
/// <example>
/// <code>
/// var symbol = new McpApiReferenceIndexSymbol { Uid = "BridgingIT.DevKit.Common.Result" };
/// </code>
/// </example>
public sealed record McpApiReferenceIndexSymbol
{
    /// <summary>
    /// Gets or initializes the DocFX uid.
    /// </summary>
    public string Uid { get; init; }

    /// <summary>
    /// Gets or initializes the short symbol name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or initializes the full symbol name.
    /// </summary>
    public string FullName { get; init; }

    /// <summary>
    /// Gets or initializes the DocFX symbol kind.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets or initializes the namespace.
    /// </summary>
    public string Namespace { get; init; }

    /// <summary>
    /// Gets or initializes the assembly.
    /// </summary>
    public string Assembly { get; init; }

    /// <summary>
    /// Gets or initializes the summary.
    /// </summary>
    public string Summary { get; init; }

    /// <summary>
    /// Gets or initializes the API reference HTML path.
    /// </summary>
    public string Href { get; init; }

    /// <summary>
    /// Gets or initializes the generated symbol detail path.
    /// </summary>
    public string Detail { get; init; }

    /// <summary>
    /// Gets or initializes inferred topics.
    /// </summary>
    public IReadOnlyList<string> Topics { get; init; } = [];
}

/// <summary>
/// Represents one detailed API reference symbol payload.
/// </summary>
/// <example>
/// <code>
/// var symbol = new McpApiReferenceSymbol { Uid = "BridgingIT.DevKit.Common.Result" };
/// </code>
/// </example>
public sealed record McpApiReferenceSymbol
{
    /// <summary>
    /// Gets or initializes the DocFX uid.
    /// </summary>
    public string Uid { get; init; }

    /// <summary>
    /// Gets or initializes the short symbol name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or initializes the full symbol name.
    /// </summary>
    public string FullName { get; init; }

    /// <summary>
    /// Gets or initializes the DocFX symbol kind.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets or initializes the namespace.
    /// </summary>
    public string Namespace { get; init; }

    /// <summary>
    /// Gets or initializes the assembly.
    /// </summary>
    public string Assembly { get; init; }

    /// <summary>
    /// Gets or initializes the summary.
    /// </summary>
    public string Summary { get; init; }

    /// <summary>
    /// Gets or initializes remarks.
    /// </summary>
    public string Remarks { get; init; }

    /// <summary>
    /// Gets or initializes the syntax object.
    /// </summary>
    public JsonElement? Syntax { get; init; }

    /// <summary>
    /// Gets or initializes parameters.
    /// </summary>
    public IReadOnlyList<McpApiReferenceParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Gets or initializes return information.
    /// </summary>
    public McpApiReferenceReturn Returns { get; init; }

    /// <summary>
    /// Gets or initializes the parent uid.
    /// </summary>
    public string Parent { get; init; }

    /// <summary>
    /// Gets or initializes child uids.
    /// </summary>
    public IReadOnlyList<string> Children { get; init; } = [];

    /// <summary>
    /// Gets or initializes extension method uids.
    /// </summary>
    public IReadOnlyList<string> ExtensionMethods { get; init; } = [];

    /// <summary>
    /// Gets or initializes examples.
    /// </summary>
    public IReadOnlyList<string> Examples { get; init; } = [];

    /// <summary>
    /// Gets or initializes the public API reference URL.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Gets or initializes inferred topics.
    /// </summary>
    public IReadOnlyList<string> Topics { get; init; } = [];
}

/// <summary>
/// Represents a method or constructor parameter in an API reference symbol.
/// </summary>
/// <example>
/// <code>
/// var parameter = new McpApiReferenceParameter { Id = "request" };
/// </code>
/// </example>
public sealed record McpApiReferenceParameter
{
    /// <summary>
    /// Gets or initializes the parameter id.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets or initializes the parameter type.
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Gets or initializes the parameter description.
    /// </summary>
    public string Description { get; init; }
}

/// <summary>
/// Represents return information in an API reference symbol.
/// </summary>
/// <example>
/// <code>
/// var returns = new McpApiReferenceReturn { Type = "Result" };
/// </code>
/// </example>
public sealed record McpApiReferenceReturn
{
    /// <summary>
    /// Gets or initializes the return type.
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Gets or initializes the return description.
    /// </summary>
    public string Description { get; init; }
}

/// <summary>
/// Reads DevKit API reference metadata from local Pages output or GitHub Pages.
/// </summary>
/// <example>
/// <code>
/// services.AddSingleton&lt;IMcpApiReferenceSource, GitHubPagesMcpApiReferenceSource&gt;();
/// </code>
/// </example>
public sealed class GitHubPagesMcpApiReferenceSource(HttpClient httpClient, CliRuntimeContext context) : IMcpApiReferenceSource
{
    private const string BaseUrl = "https://bridgingit-gmbh.github.io/bITdevKit/api/";
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private McpApiReferenceIndex cache;
    private DateTimeOffset cacheCreatedAt;

    /// <inheritdoc />
    public string Name => "official GitHub Pages API reference";

    /// <inheritdoc />
    public async Task<McpApiReferenceIndex> GetIndexAsync(CancellationToken cancellationToken)
    {
        await this.cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.cache is not null && DateTimeOffset.UtcNow - this.cacheCreatedAt < TimeSpan.FromMinutes(10))
            {
                return this.cache;
            }

            var indexJson = await this.GetIndexJsonAsync(cancellationToken).ConfigureAwait(false);
            this.cache = JsonSerializer.Deserialize<McpApiReferenceIndex>(indexJson, CliJson.Options) ??
                new McpApiReferenceIndex { SchemaVersion = 1, SiteUrl = BaseUrl };
            this.cacheCreatedAt = DateTimeOffset.UtcNow;

            return this.cache;
        }
        finally
        {
            this.cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<McpApiReferenceSymbol> GetSymbolAsync(string uid, CancellationToken cancellationToken)
    {
        var index = await this.GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var entry = index.Symbols.FirstOrDefault(symbol => string.Equals(symbol.Uid, uid, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        var json = await this.GetDetailJsonAsync(entry.Detail, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("symbols", out _))
        {
            var page = JsonSerializer.Deserialize<McpApiReferenceSymbolPage>(json, CliJson.Options);
            return page?.Symbols.FirstOrDefault(symbol => string.Equals(symbol.Uid, uid, StringComparison.OrdinalIgnoreCase)) ??
                CreateSymbolFromIndex(index, entry);
        }

        var symbol = JsonSerializer.Deserialize<McpApiReferenceSymbol>(json, CliJson.Options);
        return string.Equals(symbol?.Uid, uid, StringComparison.OrdinalIgnoreCase)
            ? symbol
            : CreateSymbolFromIndex(index, entry);
    }

    private static McpApiReferenceSymbol CreateSymbolFromIndex(McpApiReferenceIndex index, McpApiReferenceIndexSymbol entry)
        => new()
        {
            Uid = entry.Uid,
            Name = entry.Name,
            FullName = entry.FullName,
            Kind = entry.Kind,
            Namespace = entry.Namespace,
            Assembly = entry.Assembly,
            Summary = entry.Summary,
            Url = CombineUrl(index.SiteUrl, entry.Href),
            Topics = entry.Topics
        };

    private async Task<string> GetIndexJsonAsync(CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(context.Workspace.Path, ".github", "pages", "api", "agent-index.json");
        if (File.Exists(localPath))
        {
            return await File.ReadAllTextAsync(localPath, cancellationToken).ConfigureAwait(false);
        }

        return await this.GetStringAsync(BaseUrl + "agent-index.json", cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetDetailJsonAsync(string detail, CancellationToken cancellationToken)
    {
        var normalized = (detail ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
        var localPath = Path.Combine(context.Workspace.Path, ".github", "pages", "api", normalized);
        if (File.Exists(localPath))
        {
            return await File.ReadAllTextAsync(localPath, cancellationToken).ConfigureAwait(false);
        }

        return await this.GetStringAsync(CombineUrl(BaseUrl, detail), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("bdk-mcp", typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CombineUrl(string root, string relative)
        => $"{(root ?? string.Empty).TrimEnd('/')}/{(relative ?? string.Empty).TrimStart('/')}";
}

/// <summary>
/// Represents a generated API reference page-detail payload.
/// </summary>
/// <example>
/// <code>
/// var page = new McpApiReferenceSymbolPage { SchemaVersion = 1 };
/// </code>
/// </example>
public sealed record McpApiReferenceSymbolPage
{
    /// <summary>
    /// Gets or initializes the schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets or initializes the source format.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets or initializes the source file name.
    /// </summary>
    public string File { get; init; }

    /// <summary>
    /// Gets or initializes symbols on the page.
    /// </summary>
    public IReadOnlyList<McpApiReferenceSymbol> Symbols { get; init; } = [];
}