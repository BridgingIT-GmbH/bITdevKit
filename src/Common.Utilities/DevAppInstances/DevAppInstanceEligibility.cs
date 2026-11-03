// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

/// <summary>
/// Evaluates the execution-context eligibility conditions of development instance isolation.
/// </summary>
/// <example>
/// <code>
/// var reason = DevAppInstanceEligibility.Evaluate(DevAppInstanceMode.Auto, "Development", Environment.GetEnvironmentVariable);
/// var eligible = reason is null;
/// </code>
/// </example>
public static class DevAppInstanceEligibility
{
    /// <summary>
    /// Returns the inactive reason, or <c>null</c> when the process may continue with Git detection.
    /// </summary>
    /// <param name="mode">The configured mode.</param>
    /// <param name="environmentName">The host environment name.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable.</param>
    /// <param name="allowOutsideDevelopment">Bypasses the environment, container and cloud checks. Intended for tests.</param>
    /// <param name="isBuildTime">Overrides build-time detection. Intended for tests.</param>
    /// <returns>The inactive reason or <c>null</c>.</returns>
    public static string Evaluate(
        DevAppInstanceMode mode,
        string environmentName,
        Func<string, string> getEnvironmentVariable,
        bool allowOutsideDevelopment = false,
        bool? isBuildTime = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        if (mode == DevAppInstanceMode.Disabled)
        {
            return DevAppInstanceInactiveReasons.Disabled;
        }

        if (isBuildTime ?? EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
        {
            return DevAppInstanceInactiveReasons.BuildTime;
        }

        if (allowOutsideDevelopment)
        {
            return null;
        }

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return DevAppInstanceInactiveReasons.NonDevelopment;
        }

        if (string.Equals(getEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(getEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        {
            return DevAppInstanceInactiveReasons.Container;
        }

        if (!string.IsNullOrEmpty(getEnvironmentVariable("WEBSITE_SITE_NAME")))
        {
            return DevAppInstanceInactiveReasons.Cloud;
        }

        return null;
    }
}

/// <summary>
/// Probes local TCP ports for development instance allocation.
/// </summary>
/// <example>
/// <code>
/// IDevAppInstancePortProbe probe = new LoopbackPortProbe();
/// var free = probe.IsBindable(5211) &amp;&amp; !probe.IsReserved(5211);
/// </code>
/// </example>
public interface IDevAppInstancePortProbe
{
    /// <summary>
    /// Returns whether the port can be bound on loopback at this moment.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns><c>true</c> when the port is bindable.</returns>
    bool IsBindable(int port);

    /// <summary>
    /// Returns whether the port is in an OS-reserved range where the platform exposes one.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns><c>true</c> when the port is reserved.</returns>
    bool IsReserved(int port);
}

/// <summary>
/// Probes ports by binding loopback listeners and, on Windows, by reading the excluded TCP port ranges.
/// </summary>
/// <example>
/// <code>
/// var probe = new LoopbackPortProbe();
/// Console.WriteLine(probe.IsBindable(5111));
/// </code>
/// </example>
public sealed partial class LoopbackPortProbe : IDevAppInstancePortProbe
{
    private readonly Lazy<IReadOnlyList<(int Start, int End)>> excludedRanges = new(ReadExcludedRanges);

    /// <inheritdoc />
    public bool IsBindable(int port) =>
        TryBind(IPAddress.Loopback, port) && (!Socket.OSSupportsIPv6 || TryBind(IPAddress.IPv6Loopback, port));

    /// <inheritdoc />
    public bool IsReserved(int port) => this.excludedRanges.Value.Any(r => port >= r.Start && port <= r.End);

    private static bool TryBind(IPAddress address, int port)
    {
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (OperatingSystem.IsWindows())
            {
                socket.ExclusiveAddressUse = true;
            }

            socket.Bind(new IPEndPoint(address, port));
            socket.Listen(1);

            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported || ex.SocketErrorCode == SocketError.AddressNotAvailable)
        {
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static IReadOnlyList<(int Start, int End)> ReadExcludedRanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", "int ipv4 show excludedportrange protocol=tcp")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return [];
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                }

                return [];
            }

            return RangeRegex().Matches(outputTask.GetAwaiter().GetResult())
                .Select(m => (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)))
                .ToList();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"^\s*(\d+)\s+(\d+)", RegexOptions.Multiline)]
    private static partial Regex RangeRegex();
}

/// <summary>
/// The worktree-local assignment document <c>.dev/instance.json</c>.
/// </summary>
/// <param name="WorktreeId">The worktree ID.</param>
/// <param name="Slot">The slot.</param>
/// <example>
/// <code>
/// var assignment = DevAppInstanceLocalAssignment.Read(worktreeRoot);
/// </code>
/// </example>
public sealed record DevAppInstanceLocalAssignment(Guid WorktreeId, int Slot)
{
    /// <summary>
    /// Reads the assignment file, or returns <c>null</c> when absent.
    /// </summary>
    /// <param name="worktreeRoot">The worktree root.</param>
    /// <returns>The assignment or <c>null</c>.</returns>
    /// <exception cref="DevAppInstanceException">Thrown with <c>assignment_invalid</c> for invalid content.</exception>
    public static DevAppInstanceLocalAssignment Read(string worktreeRoot)
    {
        var path = DevAppInstancePaths.GetAssignmentFile(worktreeRoot);
        var node = DevAppInstanceFiles.ReadJsonObject(path);
        if (node is null)
        {
            return null;
        }

        DevAppInstanceFiles.EnsureVersion(node, path);
        try
        {
            if (Guid.TryParse(node["worktreeId"]?.GetValue<string>(), out var id) && id != Guid.Empty &&
                node["slot"]?.GetValue<int>() is { } slot && slot > 0)
            {
                return new DevAppInstanceLocalAssignment(id, slot);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw Invalid(path, ex);
        }

        throw Invalid(path, null);
    }

    /// <summary>
    /// Writes the assignment file and keeps unknown properties of an existing file.
    /// </summary>
    /// <param name="worktreeRoot">The worktree root.</param>
    public void Write(string worktreeRoot)
    {
        var path = DevAppInstancePaths.GetAssignmentFile(worktreeRoot);
        System.Text.Json.Nodes.JsonObject node;
        try
        {
            node = DevAppInstanceFiles.ReadJsonObject(path) ?? [];
        }
        catch (DevAppInstanceException)
        {
            node = [];
        }

        node["version"] = 1;
        node["worktreeId"] = this.WorktreeId.ToString("D");
        node["slot"] = this.Slot;
        DevAppInstanceFiles.WriteJsonAtomic(path, node);
    }

    private static DevAppInstanceException Invalid(string path, Exception inner) =>
        new(
            DevAppInstanceErrorCodes.AssignmentInvalid,
            $"The assignment file '{path}' has no valid worktree ID or slot.",
            "Delete the file to recover the assignment from the registry, or restore it.",
            inner);
}
