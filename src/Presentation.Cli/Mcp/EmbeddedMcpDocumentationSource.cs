namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Provides the Markdown documentation embedded in the installed CLI assembly.
/// </summary>
internal sealed class EmbeddedMcpDocumentationSource(EmbeddedMcpResources resources) : IMcpDocumentationSource, IDisposable
{
    private const string WebRoot = "https://github.com/bridgingit/bitdevkit/blob/main/";
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private IReadOnlyList<McpDocumentationDocument> cache;

    /// <inheritdoc />
    public string Name => "embedded";

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpDocumentationDocument>> ListAsync(CancellationToken cancellationToken)
    {
        await this.cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.cache is not null)
            {
                return this.cache;
            }

            var paths = resources.GetDocumentPaths();
            if (paths.Count == 0)
            {
                throw new FileNotFoundException("Embedded DevKit Markdown documentation is unavailable. Rebuild or reinstall the local MCP tool.");
            }

            var documents = new List<McpDocumentationDocument>(paths.Count);
            foreach (var path in paths)
            {
                var content = await resources.ReadTextAsync(path, cancellationToken).ConfigureAwait(false);
                documents.Add(new McpDocumentationDocument(path, WebRoot + path, content));
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.cache = Array.AsReadOnly(documents.ToArray());
            return this.cache;
        }
        finally
        {
            this.cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<McpDocumentationDocument> GetAsync(string source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = NormalizeSource(source);
        if (path is null || !resources.Contains(path))
        {
            return null;
        }

        var documents = await this.ListAsync(cancellationToken).ConfigureAwait(false);
        return documents.FirstOrDefault(document => string.Equals(document.Source, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void Dispose() => this.cacheLock.Dispose();

    private static string NormalizeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var value = source.Trim();
        var suffixIndex = value.IndexOfAny(['?', '#']);
        if (suffixIndex >= 0)
        {
            value = value[..suffixIndex];
        }

        value = DecodePath(value);
        if (value is null)
        {
            return null;
        }

        if (value.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return value.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? value : null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
        {
            return null;
        }

        var isBlobUrl = string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
        var isRawUrl = string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        if (!isBlobUrl && !isRawUrl)
        {
            return null;
        }

        var segments = Uri.UnescapeDataString(uri.AbsolutePath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var minimumDocsIndex = isBlobUrl ? 4 : 3;
        if (segments.Length <= minimumDocsIndex + 1 ||
            (!string.Equals(segments[0], "bridgingit", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(segments[0], "bridgingit-gmbh", StringComparison.OrdinalIgnoreCase)) ||
            !string.Equals(segments[1], "bitdevkit", StringComparison.OrdinalIgnoreCase) ||
            (isBlobUrl && !string.Equals(segments[2], "blob", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var docsIndex = Array.FindLastIndex(segments, segment => string.Equals(segment, "docs", StringComparison.OrdinalIgnoreCase));
        if (docsIndex < minimumDocsIndex)
        {
            return null;
        }

        var path = string.Join('/', segments[docsIndex..]);
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private static string DecodePath(string value)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (value.Contains('\\') || value.Any(char.IsControl) || value.Split('/').Any(segment => segment is "." or ".."))
            {
                return null;
            }

            var decoded = Uri.UnescapeDataString(value);
            if (decoded == value)
            {
                return value;
            }

            value = decoded;
        }

        return null;
    }
}
