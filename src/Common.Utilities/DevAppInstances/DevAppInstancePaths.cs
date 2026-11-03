// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Computes the well-known development instance file locations.
/// </summary>
/// <example>
/// <code>
/// var registry = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory);
/// </code>
/// </example>
public static class DevAppInstancePaths
{
    /// <summary>Gets the worktree-local directory name.</summary>
    public const string LocalDirectoryName = ".dev";

    /// <summary>Gets the registry directory name below the common Git directory.</summary>
    public const string RegistryDirectoryName = "bdk-dev";

    /// <summary>Returns <c>&lt;commonDir&gt;/bdk-dev</c>.</summary>
    /// <param name="commonDirectory">The common Git directory.</param>
    /// <returns>The path.</returns>
    public static string GetRegistryDirectory(string commonDirectory) => Path.Combine(commonDirectory, RegistryDirectoryName);

    /// <summary>Returns <c>&lt;commonDir&gt;/bdk-dev/instances.json</c>.</summary>
    /// <param name="commonDirectory">The common Git directory.</param>
    /// <returns>The path.</returns>
    public static string GetRegistryFile(string commonDirectory) => Path.Combine(GetRegistryDirectory(commonDirectory), "instances.json");

    /// <summary>Returns <c>&lt;commonDir&gt;/bdk-dev/instances.lock</c>.</summary>
    /// <param name="commonDirectory">The common Git directory.</param>
    /// <returns>The path.</returns>
    public static string GetRegistryLockFile(string commonDirectory) => Path.Combine(GetRegistryDirectory(commonDirectory), "instances.lock");

    /// <summary>Returns <c>&lt;commonDir&gt;/bdk-dev/leases/&lt;worktreeId&gt;.&lt;appId&gt;.lock</c>.</summary>
    /// <param name="commonDirectory">The common Git directory.</param>
    /// <param name="worktreeId">The worktree ID.</param>
    /// <param name="appId">The application ID.</param>
    /// <returns>The path.</returns>
    public static string GetLeaseFile(string commonDirectory, Guid worktreeId, string appId) =>
        Path.Combine(GetRegistryDirectory(commonDirectory), "leases", $"{worktreeId:D}.{appId}.lock");

    /// <summary>Returns <c>&lt;worktree&gt;/.dev/instance.json</c>.</summary>
    /// <param name="worktreeRoot">The worktree root.</param>
    /// <returns>The path.</returns>
    public static string GetAssignmentFile(string worktreeRoot) => Path.Combine(worktreeRoot, LocalDirectoryName, "instance.json");

    /// <summary>Returns <c>&lt;worktree&gt;/.dev/state/&lt;appId&gt;</c>.</summary>
    /// <param name="worktreeRoot">The worktree root.</param>
    /// <param name="appId">The application ID.</param>
    /// <returns>The path.</returns>
    public static string GetStateRoot(string worktreeRoot, string appId) => Path.Combine(worktreeRoot, LocalDirectoryName, "state", appId);

    /// <summary>Returns the inventory file of an application state root.</summary>
    /// <param name="stateRoot">The state root.</param>
    /// <returns>The path.</returns>
    public static string GetInventoryFile(string stateRoot) => Path.Combine(stateRoot, "inventory.json");

    /// <summary>Returns the ownership file of an application state root.</summary>
    /// <param name="stateRoot">The state root.</param>
    /// <returns>The path.</returns>
    public static string GetOwnerFile(string stateRoot) => Path.Combine(stateRoot, ".bdk-owner.json");
}

/// <summary>
/// Provides the registry lock, atomic replacement and JSON helpers shared by hosts and the <c>bdk dev</c> commands.
/// </summary>
/// <example>
/// <code>
/// using (DevAppInstanceFiles.AcquireLock(lockPath, TimeSpan.FromSeconds(10)))
/// {
///     DevAppInstanceFiles.WriteJsonAtomic(path, node);
/// }
/// </code>
/// </example>
public static class DevAppInstanceFiles
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Acquires an exclusive cross-process lock file.
    /// </summary>
    /// <param name="lockPath">The lock file path.</param>
    /// <param name="timeout">The maximum wait.</param>
    /// <param name="errorCode">The error code used on timeout.</param>
    /// <param name="onRetry">An optional callback invoked for each failed attempt.</param>
    /// <returns>The held lock. Dispose it to release.</returns>
    /// <exception cref="DevAppInstanceException">Thrown when the lock is not acquired within <paramref name="timeout"/>.</exception>
    public static FileStream AcquireLock(string lockPath, TimeSpan timeout, string errorCode = DevAppInstanceErrorCodes.RegistryLocked, Action<int> onRetry = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var deadline = DateTime.UtcNow + timeout;
        var attempt = 0;
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                attempt++;
                onRetry?.Invoke(attempt);
                Thread.Sleep(100);
            }
            catch (IOException ex)
            {
                throw new DevAppInstanceException(
                    errorCode,
                    $"The lock '{lockPath}' is held by another process.",
                    errorCode == DevAppInstanceErrorCodes.RegistryLocked
                        ? "Retry when the other startup or bdk dev command has finished."
                        : "Stop the running instance (bdk dev stop) or wait until it has exited.",
                    ex);
            }
        }
    }

    /// <summary>
    /// Reads a JSON object file, or returns <c>null</c> when the file does not exist.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The JSON object or <c>null</c>.</returns>
    /// <exception cref="DevAppInstanceException">Thrown with <c>assignment_invalid</c> on invalid JSON.</exception>
    public static JsonObject ReadJsonObject(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new JsonException("The document is not a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.AssignmentInvalid,
                $"The file '{path}' is not valid JSON.",
                "Restore the file from a backup, or delete it so each worktree gets a slot again on its next start (slots and ports may change).",
                ex);
        }
    }

    /// <summary>
    /// Writes a JSON node through a temporary file and an atomic replace.
    /// </summary>
    /// <param name="path">The target path.</param>
    /// <param name="node">The node to write.</param>
    public static void WriteJsonAtomic(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, node.ToJsonString(WriteOptions), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Ensures a document version is supported.
    /// </summary>
    /// <param name="node">The document.</param>
    /// <param name="path">The document path used in errors.</param>
    /// <param name="supportedVersion">The highest supported version.</param>
    /// <exception cref="DevAppInstanceException">Thrown with <c>assignment_invalid</c> for a missing or higher version.</exception>
    public static void EnsureVersion(JsonObject node, string path, int supportedVersion = 1)
    {
        var version = node["version"]?.GetValueKind() == JsonValueKind.Number ? node["version"]!.GetValue<int>() : 0;
        if (version < 1)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.AssignmentInvalid,
                $"The file '{path}' has no valid version.",
                "Restore the file from a backup, or delete it so each worktree gets a slot again on its next start (slots and ports may change).");
        }

        if (version > supportedVersion)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.AssignmentInvalid,
                $"The file '{path}' has version {version}, but this DevKit version supports version {supportedVersion}.",
                "Update the DevKit packages and the bdk tool to the version that wrote this file.");
        }
    }
}
