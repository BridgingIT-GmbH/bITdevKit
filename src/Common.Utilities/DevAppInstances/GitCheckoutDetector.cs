// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Identifies the kind of Git checkout that contains a directory.
/// </summary>
public enum GitCheckoutKind
{
    /// <summary>The directory is not inside a Git checkout.</summary>
    OutsideGit = 0,

    /// <summary>The directory is inside an ordinary checkout, submodule, or separate-Git-directory checkout.</summary>
    Ordinary = 1,

    /// <summary>The directory is inside a Git linked worktree.</summary>
    LinkedWorktree = 2,
}

/// <summary>
/// Describes the Git checkout that contains a directory.
/// </summary>
/// <param name="Kind">The checkout kind.</param>
/// <param name="WorktreeRoot">The canonical checkout root, or <c>null</c> outside Git.</param>
/// <param name="GitDirectory">The canonical per-checkout Git directory, or <c>null</c> outside Git.</param>
/// <param name="CommonDirectory">The canonical shared common Git directory, or <c>null</c> outside Git.</param>
/// <example>
/// <code>
/// var checkout = GitCheckoutDetector.Detect(contentRoot);
/// if (checkout.Kind == GitCheckoutKind.LinkedWorktree)
/// {
///     Console.WriteLine(checkout.CommonDirectory);
/// }
/// </code>
/// </example>
public sealed record GitCheckoutInfo(GitCheckoutKind Kind, string WorktreeRoot, string GitDirectory, string CommonDirectory)
{
    /// <summary>
    /// Gets a value indicating whether the checkout is a linked worktree.
    /// </summary>
    public bool IsLinkedWorktree => this.Kind == GitCheckoutKind.LinkedWorktree;

    /// <summary>
    /// Gets the reason why unusable Git metadata was ignored, or <c>null</c> when detection was conclusive.
    /// </summary>
    /// <example>
    /// <code>
    /// if (checkout.Warning is not null) { logger.LogWarning("{Warning}", checkout.Warning); }
    /// </code>
    /// </example>
    public string Warning { get; init; }

    /// <summary>
    /// Gets the result for a directory outside Git.
    /// </summary>
    public static GitCheckoutInfo Outside { get; } = new(GitCheckoutKind.OutsideGit, null, null, null);
}

/// <summary>
/// Executes read-only Git commands for inconclusive checkout detection.
/// </summary>
/// <example>
/// <code>
/// var detector = GitCheckoutDetector.Detect(path, new ProcessGitCommandRunner());
/// </code>
/// </example>
public interface IGitCommandRunner
{
    /// <summary>
    /// Runs one Git command and returns trimmed standard output, or <c>null</c> on failure or timeout.
    /// </summary>
    /// <param name="workingDirectory">The directory passed through <c>-C</c>.</param>
    /// <param name="arguments">The structured Git arguments.</param>
    /// <returns>The trimmed standard output or <c>null</c>.</returns>
    string Run(string workingDirectory, params string[] arguments);
}

/// <summary>
/// Runs Git through <see cref="Process"/> with structured arguments, a five-second timeout and a cleared Git environment.
/// </summary>
/// <example>
/// <code>
/// var topLevel = new ProcessGitCommandRunner().Run(path, "rev-parse", "--show-toplevel");
/// </code>
/// </example>
public sealed class ProcessGitCommandRunner : IGitCommandRunner
{
    private static readonly string[] ClearedVariables = ["GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_CEILING_DIRECTORIES"];

    /// <summary>
    /// Gets or sets the per-command timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public string Run(string workingDirectory, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(workingDirectory);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var variable in ClearedVariables)
            {
                startInfo.Environment.Remove(variable);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(this.Timeout))
            {
                try
                {
                    process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            return process.ExitCode == 0 ? output.GetAwaiter().GetResult().Trim() : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Git is missing, blocked or unsupported on this platform. Callers treat null as "Git could not confirm".
            return null;
        }
    }
}

/// <summary>
/// Detects ordinary checkouts and linked worktrees from Git's documented on-disk layout.
/// </summary>
/// <remarks>
/// Detection ignores <c>GIT_DIR</c>, <c>GIT_WORK_TREE</c> and <c>GIT_COMMON_DIR</c>. A Git process is started only when the
/// files are inconclusive and a runner is supplied. Design-time callers pass no runner. Detection needs neither a Git
/// installation nor a repository: a directory without <c>.git</c> is <see cref="GitCheckoutKind.OutsideGit"/>. Unusable
/// metadata without linked-worktree evidence is also reported as outside Git, with <see cref="GitCheckoutInfo.Warning"/>
/// set. Only unconfirmable linked-worktree metadata fails with <c>worktree_detection_failed</c>.
/// </remarks>
/// <example>
/// <code>
/// var checkout = GitCheckoutDetector.Detect(builder.Environment.ContentRootPath, new ProcessGitCommandRunner());
/// </code>
/// </example>
public static class GitCheckoutDetector
{
    /// <summary>
    /// Gets the string comparer for canonical paths on the current platform.
    /// </summary>
    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Detects the checkout that contains <paramref name="startDirectory"/>.
    /// </summary>
    /// <param name="startDirectory">The directory to start from, for example the host content root.</param>
    /// <param name="gitRunner">The optional Git runner used for inconclusive metadata.</param>
    /// <returns>The detected checkout information.</returns>
    /// <exception cref="DevAppInstanceException">Thrown with <c>worktree_detection_failed</c> when metadata is inconclusive.</exception>
    public static GitCheckoutInfo Detect(string startDirectory, IGitCommandRunner gitRunner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var dotGit = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(dotGit))
            {
                var root = Canonicalize(directory.FullName);
                var gitDir = Canonicalize(dotGit);

                return new GitCheckoutInfo(GitCheckoutKind.Ordinary, root, gitDir, gitDir);
            }

            if (File.Exists(dotGit))
            {
                return DetectFromGitFile(directory.FullName, dotGit, gitRunner);
            }

            directory = directory.Parent;
        }

        return GitCheckoutInfo.Outside;
    }

    /// <summary>
    /// Returns the canonical form of a path with symbolic links resolved and trailing separators removed.
    /// </summary>
    /// <param name="path">The path to canonicalize.</param>
    /// <returns>The canonical path.</returns>
    public static string Canonicalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var full = Path.GetFullPath(path);
        try
        {
            var info = Directory.Exists(full) ? (FileSystemInfo)new DirectoryInfo(full) : new FileInfo(full);
            var target = info.ResolveLinkTarget(true);
            if (target is not null)
            {
                full = Path.GetFullPath(target.FullName);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Path.TrimEndingDirectorySeparator(full);
    }

    /// <summary>
    /// Compares two paths using host filesystem semantics after canonicalization.
    /// </summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><c>true</c> when both paths identify the same location.</returns>
    public static bool PathsEqual(string left, string right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return PathComparer.Equals(Canonicalize(left), Canonicalize(right));
    }

    private static GitCheckoutInfo DetectFromGitFile(string checkoutDirectory, string dotGitFile, IGitCommandRunner gitRunner)
    {
        var root = Canonicalize(checkoutDirectory);
        string gitDirLine;
        try
        {
            gitDirLine = File.ReadAllLines(dotGitFile).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fallback(root, gitRunner, $"The .git file in '{root}' cannot be read.", worktreeEvidence: false);
        }

        const string prefix = "gitdir:";
        if (gitDirLine is null || !gitDirLine.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Fallback(root, gitRunner, $"The .git file in '{root}' has no 'gitdir:' line.", worktreeEvidence: false);
        }

        string gitDir;
        try
        {
            var gitDirValue = gitDirLine[prefix.Length..].Trim();
            gitDir = Path.GetFullPath(Path.IsPathRooted(gitDirValue) ? gitDirValue : Path.Combine(checkoutDirectory, gitDirValue));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Fallback(root, gitRunner, $"The .git file in '{root}' has an invalid 'gitdir:' path.", worktreeEvidence: false);
        }

        if (!Directory.Exists(gitDir))
        {
            // Git keeps linked-worktree metadata in <common>/worktrees/<name>; anything else is not worktree evidence.
            var looksLinked = string.Equals(Path.GetFileName(Path.GetDirectoryName(gitDir)), "worktrees", StringComparison.OrdinalIgnoreCase);

            return Fallback(root, gitRunner, $"The Git directory '{gitDir}' referenced by '{root}' does not exist.", worktreeEvidence: looksLinked);
        }

        var commonDirFile = Path.Combine(gitDir, "commondir");
        if (!File.Exists(commonDirFile))
        {
            var canonicalGitDir = Canonicalize(gitDir);

            return new GitCheckoutInfo(GitCheckoutKind.Ordinary, root, canonicalGitDir, canonicalGitDir);
        }

        try
        {
            var commonValue = File.ReadAllText(commonDirFile).Trim();
            var commonDir = Path.GetFullPath(Path.IsPathRooted(commonValue) ? commonValue : Path.Combine(gitDir, commonValue));
            var backPointerFile = Path.Combine(gitDir, "gitdir");
            if (!File.Exists(backPointerFile) || !Directory.Exists(commonDir))
            {
                return Fallback(root, gitRunner, $"The linked worktree metadata in '{gitDir}' is incomplete.");
            }

            var backValue = File.ReadAllText(backPointerFile).Trim();
            var backPointer = Path.GetFullPath(Path.IsPathRooted(backValue) ? backValue : Path.Combine(gitDir, backValue));
            if (!PathsEqual(backPointer, dotGitFile))
            {
                return Fallback(root, gitRunner, $"The worktree back-pointer '{backPointer}' does not name '{dotGitFile}'.");
            }

            return new GitCheckoutInfo(GitCheckoutKind.LinkedWorktree, root, Canonicalize(gitDir), Canonicalize(commonDir));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fallback(root, gitRunner, $"The linked worktree metadata in '{gitDir}' cannot be read.");
        }
    }

    // Inconclusive metadata asks Git once. Without linked-worktree evidence an unconfirmed checkout is treated as outside
    // Git, so a missing Git installation or a stray .git file never blocks the host. Linked-worktree evidence fails
    // closed, because starting unisolated there would use the main checkout's ports and databases.
    private static GitCheckoutInfo Fallback(string root, IGitCommandRunner gitRunner, string reason, bool worktreeEvidence = true)
    {
        const string recovery = "Run 'git worktree repair' in the worktree, or set DevAppInstance__Mode=Disabled to start with the configured ports and databases. Design-time tools accept '--connection-string='.";
        string failure;
        var topLevel = gitRunner?.Run(root, "rev-parse", "--show-toplevel");
        if (topLevel is null)
        {
            failure = gitRunner is null ? null : "Git did not confirm a repository here, or Git is not installed.";
        }
        else
        {
            var gitDir = gitRunner.Run(root, "rev-parse", "--absolute-git-dir");
            var commonDir = gitDir is null ? null : gitRunner.Run(root, "rev-parse", "--path-format=absolute", "--git-common-dir");
            if (gitDir is null || commonDir is null)
            {
                failure = "Git could not confirm the checkout.";
            }
            else if (!worktreeEvidence || PathsEqual(topLevel, root))
            {
                var kind = PathsEqual(gitDir, commonDir) ? GitCheckoutKind.Ordinary : GitCheckoutKind.LinkedWorktree;

                return new GitCheckoutInfo(kind, Canonicalize(topLevel), Canonicalize(gitDir), Canonicalize(commonDir));
            }
            else
            {
                failure = $"Git reports top level '{topLevel}' instead of '{root}'.";
            }
        }

        var message = failure is null ? reason : $"{reason} {failure}";
        if (!worktreeEvidence)
        {
            return GitCheckoutInfo.Outside with { Warning = message };
        }

        throw new DevAppInstanceException(DevAppInstanceErrorCodes.WorktreeDetectionFailed, message, recovery);
    }
}
