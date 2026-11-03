namespace BridgingIT.DevKit.Cli;

using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;

/// <summary>
/// A DevKit web host project that <c>bdk dev run</c> can start.
/// </summary>
/// <param name="AppId">The application ID.</param>
/// <param name="ProjectPath">The project directory relative to the worktree root, using forward slashes.</param>
/// <param name="ProjectFile">The full path of the project file.</param>
/// <param name="Source"><c>registry</c> when a host start recorded the project, <c>discovered</c> when the scan found it.</param>
public sealed record DevAppInstanceHostProject(string AppId, string ProjectPath, string ProjectFile, string Source);

/// <summary>
/// Finds the DevKit web host projects of a checkout.
/// </summary>
/// <remarks>
/// Projects recorded in the registry inventory come first, because their application ID is the one the host used,
/// including IDs set with <c>WithAppId</c>. The scan then adds every <c>Microsoft.NET.Sdk.Web</c> project whose top-level
/// C# files call <c>DevKitWebApplication.CreateBuilder</c>, with the ID derived from its assembly name.
/// </remarks>
/// <example>
/// <code>
/// var projects = DevAppInstanceHostProjects.Find(worktreeRoot, registry, currentAssignment);
/// </code>
/// </example>
public static partial class DevAppInstanceHostProjects
{
    private const string BuilderCall = "DevKitWebApplication.CreateBuilder";

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "TestResults", "artifacts",
    };

    /// <summary>
    /// Finds the host projects of a checkout.
    /// </summary>
    /// <param name="worktreeRoot">The worktree root.</param>
    /// <param name="registry">The registry, or <c>null</c>.</param>
    /// <param name="current">The assignment of the checkout, or <c>null</c>.</param>
    /// <returns>The host projects, ordered by application ID.</returns>
    public static IReadOnlyList<DevAppInstanceHostProject> Find(string worktreeRoot, DevAppInstanceRegistry registry, DevAppInstanceAssignment current)
    {
        var projects = new List<DevAppInstanceHostProject>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var application in registry?.Applications ?? [])
        {
            var projectPath = (current?.Applications ?? [])
                .Concat((registry?.Assignments ?? []).SelectMany(a => a.Applications))
                .FirstOrDefault(i => string.Equals(i.AppId, application.AppId, StringComparison.Ordinal) && !string.IsNullOrEmpty(i.ProjectPath))
                ?.ProjectPath;
            var projectFile = projectPath is null ? null : FindProjectFile(Path.Combine(worktreeRoot, projectPath.Replace('/', Path.DirectorySeparatorChar)));
            if (projectFile is not null && directories.Add(Path.GetDirectoryName(projectFile)))
            {
                projects.Add(new DevAppInstanceHostProject(application.AppId, projectPath, projectFile, "registry"));
            }
        }

        foreach (var projectFile in EnumerateProjectFiles(worktreeRoot).Where(IsDevKitWebHost))
        {
            var directory = Path.GetDirectoryName(projectFile);
            if (!directories.Add(directory))
            {
                continue;
            }

            var appId = DevAppInstanceApplication.DeriveAppId(GetAssemblyName(projectFile));
            if (projects.Any(p => string.Equals(p.AppId, appId, StringComparison.Ordinal)))
            {
                continue;
            }

            projects.Add(new DevAppInstanceHostProject(appId, Path.GetRelativePath(worktreeRoot, directory).Replace('\\', '/'), projectFile, "discovered"));
        }

        return projects.OrderBy(p => p.AppId, StringComparer.Ordinal).ToList();
    }

    internal static bool IsDevKitWebHost(string projectFile)
    {
        try
        {
            if (File.ReadAllText(projectFile).IndexOf("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return Directory.EnumerateFiles(Path.GetDirectoryName(projectFile), "*.cs", SearchOption.TopDirectoryOnly)
                .Any(file => File.ReadAllText(file).Contains(BuilderCall, StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static string GetAssemblyName(string projectFile)
    {
        try
        {
            var match = AssemblyNamePattern().Match(File.ReadAllText(projectFile));
            if (match.Success && !match.Groups[1].Value.Contains('$', StringComparison.Ordinal))
            {
                return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall back to the file name.
        }

        return Path.GetFileNameWithoutExtension(projectFile);
    }

    private static string FindProjectFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var files = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);

        return files.Length == 1 ? files[0] : null;
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] files;
            string[] children;
            try
            {
                files = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
                children = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (!name.StartsWith('.') && !SkippedDirectories.Contains(name) && !IsNestedCheckout(child))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static bool IsNestedCheckout(string directory)
    {
        var git = Path.Combine(directory, ".git");

        return File.Exists(git) || Directory.Exists(git);
    }

    [GeneratedRegex(@"<AssemblyName>\s*([^<]+?)\s*</AssemblyName>", RegexOptions.IgnoreCase)]
    private static partial Regex AssemblyNamePattern();
}
