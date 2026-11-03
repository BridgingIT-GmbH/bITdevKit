// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Creates a temporary repository layout with an ordinary checkout and linked worktrees, without running Git.
/// </summary>
public sealed class FakeGitRepository : IDisposable
{
    public FakeGitRepository()
    {
        this.Root = Path.Combine(Path.GetTempPath(), "bdk-wti-" + Guid.NewGuid().ToString("N")[..8]);
        this.MainRoot = Path.Combine(this.Root, "main");
        this.CommonDirectory = Path.Combine(this.MainRoot, ".git");
        Directory.CreateDirectory(this.CommonDirectory);
    }

    public string Root { get; }

    public string MainRoot { get; }

    public string CommonDirectory { get; }

    public string AddWorktree(string name)
    {
        var worktree = Path.Combine(this.Root, name);
        var gitDir = Path.Combine(this.CommonDirectory, "worktrees", name);
        Directory.CreateDirectory(worktree);
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {gitDir}\n");
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..\n");
        File.WriteAllText(Path.Combine(gitDir, "gitdir"), Path.Combine(worktree, ".git") + "\n");

        return worktree;
    }

    public DevAppInstanceResolveRequest Request(
        string contentRoot,
        DevAppInstanceApplication application = null,
        IDevAppInstancePortProbe probe = null,
        IDictionary<string, string> settings = null,
        IReadOnlyList<DevAppInstanceResourceDeclaration> resources = null,
        IReadOnlyCollection<int> reservedPorts = null,
        IReadOnlyList<IDevAppInstanceDatabaseAdapter> databaseAdapters = null,
        IReadOnlyCollection<string> sharedKeys = null,
        string environmentName = "Development") => new()
        {
            Application = application ?? new DevAppInstanceApplication("forecasthub", httpOffset: 10, httpsOffset: 11),
            ContentRoot = contentRoot,
            EnvironmentName = environmentName,
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string>()).Build(),
            GitRunner = null,
            PortProbe = probe ?? new FakePortProbe(),
            GetEnvironmentVariable = _ => null,
            Resources = resources ?? [],
            ReservedPorts = reservedPorts ?? [],
            DatabaseAdapters = databaseAdapters ?? [],
            SharedKeys = sharedKeys ?? [],
            IsBuildTime = false,
        };

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.Root, true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>
/// A port probe that reports configured ports as blocked.
/// </summary>
public sealed class FakePortProbe(params int[] blocked) : IDevAppInstancePortProbe
{
    public HashSet<int> Blocked { get; } = [.. blocked];

    public HashSet<int> Reserved { get; } = [];

    public bool IsBindable(int port) => !this.Blocked.Contains(port);

    public bool IsReserved(int port) => this.Reserved.Contains(port);
}

/// <summary>
/// A Git runner that counts calls and returns a fixed result, for example <c>null</c> to simulate a missing Git installation.
/// </summary>
public sealed class RecordingGitRunner(string result) : IGitCommandRunner
{
    public int Calls { get; private set; }

    public string Run(string workingDirectory, params string[] arguments)
    {
        this.Calls++;

        return result;
    }
}
