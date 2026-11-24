namespace BridgingIT.DevKit.Cli;

using System.Text.Json;

/// <summary>
/// Provides the API metadata embedded in the installed CLI assembly.
/// </summary>
internal sealed class EmbeddedMcpApiReferenceSource(EmbeddedMcpResources resources) : IMcpApiReferenceSource, IDisposable
{
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private readonly Dictionary<string, IReadOnlyList<McpApiReferenceSymbol>> pages = new(StringComparer.OrdinalIgnoreCase);
    private McpApiReferenceIndex cache;

    /// <inheritdoc />
    public string Name => "embedded";

    /// <inheritdoc />
    public async Task<McpApiReferenceIndex> GetIndexAsync(CancellationToken cancellationToken)
    {
        await this.cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.cache is not null)
            {
                return this.cache;
            }

            var json = await resources.ReadTextAsync(EmbeddedMcpResources.ApiIndexPath, cancellationToken).ConfigureAwait(false);
            var index = JsonSerializer.Deserialize<McpApiReferenceIndex>(json, CliJson.Options) ??
                throw new InvalidDataException("The embedded DevKit API index could not be read.");
            cancellationToken.ThrowIfCancellationRequested();
            this.cache = index;
            return index;
        }
        finally
        {
            this.cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<McpApiReferenceSymbol> GetSymbolAsync(string uid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = await this.GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var entry = index.Symbols?.FirstOrDefault(symbol => string.Equals(symbol.Uid, uid, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        var detailPath = entry.Detail?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true
            ? entry.Detail
            : "api/" + entry.Detail;
        await this.cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!this.pages.TryGetValue(detailPath, out var symbols))
            {
                var json = await resources.ReadTextAsync(detailPath, cancellationToken).ConfigureAwait(false);
                symbols = ReadSymbols(json);
                cancellationToken.ThrowIfCancellationRequested();
                this.pages.Add(detailPath, symbols);
            }

            return symbols.FirstOrDefault(symbol => string.Equals(symbol.Uid, uid, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            this.cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.cacheLock.Dispose();

    private static IReadOnlyList<McpApiReferenceSymbol> ReadSymbols(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The embedded DevKit API detail is not an object.");
        }

        if (document.RootElement.EnumerateObject().Any(property => string.Equals(property.Name, "symbols", StringComparison.OrdinalIgnoreCase)))
        {
            var page = JsonSerializer.Deserialize<McpApiReferenceSymbolPage>(json, CliJson.Options);
            return Array.AsReadOnly((page?.Symbols ?? []).ToArray());
        }

        var symbol = JsonSerializer.Deserialize<McpApiReferenceSymbol>(json, CliJson.Options);
        return symbol is null ? [] : Array.AsReadOnly(new[] { symbol });
    }
}
