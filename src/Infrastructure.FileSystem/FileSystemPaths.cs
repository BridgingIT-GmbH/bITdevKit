namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Shares filesystem path normalization and link inspection across providers.</summary>
/// <example><code>var path = FileSystemPaths.Combine(root, "reports/daily.txt");</code></example>
public static class FileSystemPaths
{
    /// <summary>Combines a root with a file-provider path, accepting either logical separator.</summary>
    /// <remarks>Preserves the existing File Storage path semantics, including leading separators. This is normalization, not a containment or authorization check.</remarks>
    /// <example><code>var path = FileSystemPaths.Combine(root, "reports/daily.txt");</code></example>
    public static string Combine(string root, string path)
    {
        return Path.Combine(root, path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));
    }

    /// <summary>Returns a relative path with forward-slash separators for the public file-provider contract.</summary>
    /// <example><code>var relative = FileSystemPaths.Relative(root, fullPath);</code></example>
    public static string Relative(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    /// <summary>Rejects existing symlinks or reparse points in a path and its ancestors.</summary>
    /// <remarks>Requires trusted roots. The check does not prevent a hostile concurrent replacement of an inspected path.</remarks>
    /// <exception cref="InvalidDataException">An existing path component is a link or reparse point.</exception>
    /// <example><code>FileSystemPaths.RejectLinks(path);</code></example>
    public static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;

            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Unsupported link traversal.");
            }
        }
    }
}
