namespace BridgingIT.DevKit.Cli;

using System.Reflection;
using System.Text;

/// <summary>
/// Reads the documentation resources packaged in a CLI assembly.
/// </summary>
internal sealed class EmbeddedMcpResources
{
    internal const string ResourcePrefix = "BridgingIT.DevKit.Cli.Documentation/";
    internal const string ApiIndexPath = "api/agent-index.json";

    private readonly Assembly assembly;
    private readonly IReadOnlyDictionary<string, string> resourceNames;
    private readonly IReadOnlyList<string> documentPaths;

    /// <summary>
    /// Creates a resource reader for an assembly and logical resource prefix.
    /// </summary>
    public EmbeddedMcpResources(Assembly assembly, string resourcePrefix = ResourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);

        this.assembly = assembly;
        this.resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .Select(name => new { Path = name[resourcePrefix.Length..], Name = name })
            .Where(resource => IsCanonicalPath(resource.Path))
            .ToDictionary(resource => resource.Path, resource => resource.Name, StringComparer.OrdinalIgnoreCase);
        this.documentPaths = Array.AsReadOnly(this.resourceNames.Keys
            .Where(path => path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    /// <summary>
    /// Returns the canonical paths of embedded Markdown documents.
    /// </summary>
    public IReadOnlyList<string> GetDocumentPaths() => this.documentPaths;

    /// <summary>
    /// Determines whether a canonical documentation or API resource exists.
    /// </summary>
    public bool Contains(string canonicalPath)
        => IsCanonicalPath(canonicalPath) && this.resourceNames.ContainsKey(canonicalPath);

    /// <summary>
    /// Reads a resource as text without consulting the filesystem or network.
    /// </summary>
    public async Task<string> ReadTextAsync(string canonicalPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCanonicalPath(canonicalPath) || !this.resourceNames.TryGetValue(canonicalPath, out var resourceName))
        {
            throw new FileNotFoundException($"Embedded documentation resource '{canonicalPath}' was not found.");
        }

        using var stream = this.assembly.GetManifestResourceStream(resourceName) ??
            throw new FileNotFoundException($"Embedded documentation resource '{canonicalPath}' could not be read.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsCanonicalPath(string path)
        => !string.IsNullOrWhiteSpace(path) &&
            !path.Contains('\\') &&
            !path.Contains(':') &&
            !path.Any(char.IsControl) &&
            (path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("api/", StringComparison.OrdinalIgnoreCase)) &&
            path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
}
