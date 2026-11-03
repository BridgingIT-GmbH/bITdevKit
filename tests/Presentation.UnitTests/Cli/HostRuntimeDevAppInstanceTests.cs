namespace BridgingIT.DevKit.Presentation.UnitTests.Cli;

using System.Diagnostics;
using System.Text.Json;
using BridgingIT.DevKit.Cli;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

[UnitTest("Presentation")]
public sealed class HostRuntimeDevAppInstanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "bdk-host-dai-tests", Guid.NewGuid().ToString("N"));

    public HostRuntimeDevAppInstanceTests() => Directory.CreateDirectory(this.root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Discover_WhenWorkspaceIsWorktreeRootOfNestedHost_ReturnsHost()
    {
        // Arrange
        var worktreeRoot = this.CreateDirectory("worktree");
        var registryPath = this.CreateDirectory("hosts");
        WriteDescriptor(registryPath, CreateDescriptor("forecasthub-1", Path.Combine(worktreeRoot, "src"), worktreeRoot, CurrentProcessStartedAt()));
        var sut = new HostRuntimeDiscovery(new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = registryPath });

        // Act
        var result = sut.Discover(new WorkspaceResolver().Resolve(worktreeRoot), featureName: "mcp");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(HostRuntimeStatus.Ready);
    }

    [Fact]
    public void Discover_WhenHostBelongsToOtherWorktree_ExcludesHost()
    {
        // Arrange
        var worktreeRoot = this.CreateDirectory("worktree-a");
        var otherRoot = this.CreateDirectory("worktree-b");
        var registryPath = this.CreateDirectory("hosts");
        WriteDescriptor(registryPath, CreateDescriptor("forecasthub-1", otherRoot, otherRoot, CurrentProcessStartedAt()));
        var sut = new HostRuntimeDiscovery(new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = registryPath });

        // Act
        var result = sut.Discover(new WorkspaceResolver().Resolve(worktreeRoot));

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Discover_WhenProcessStartTimeDiffers_ReturnsStale()
    {
        // Arrange
        var worktreeRoot = this.CreateDirectory("worktree");
        var registryPath = this.CreateDirectory("hosts");
        WriteDescriptor(registryPath, CreateDescriptor("forecasthub-1", worktreeRoot, worktreeRoot, CurrentProcessStartedAt()!.Value.AddHours(-1)));
        var sut = new HostRuntimeDiscovery(new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = registryPath });

        // Act
        var result = sut.Discover(new WorkspaceResolver().Resolve(worktreeRoot));

        // Assert
        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(HostRuntimeStatus.Stale);
    }

    [Fact]
    public void Map_WhenDescriptorHasUrlsAndInstance_IncludesPrimaryUrlAndInstance()
    {
        // Arrange
        var descriptor = CreateDescriptor("forecasthub-1", this.root, this.root, CurrentProcessStartedAt());
        descriptor.Urls = ["https://localhost:5111", "http://localhost:5110"];
        var host = new HostRuntimeInfo { Descriptor = descriptor, Status = HostRuntimeStatus.Ready };

        // Act
        var json = JsonSerializer.SerializeToElement(HostRuntimeJson.Map(host), CliJson.Options);

        // Assert
        json.GetProperty("url").GetString().ShouldBe("https://localhost:5111");
        json.GetProperty("urls").GetArrayLength().ShouldBe(2);
        json.GetProperty("devAppInstance").GetProperty("slot").GetInt32().ShouldBe(1);
        json.GetProperty("devAppInstance").GetProperty("appId").GetString().ShouldBe("forecasthub");
    }

    [Fact]
    public void GetPrimaryUrl_WhenUrlsAreMissing_FallsBackToInstanceHttpsUrl()
    {
        // Arrange
        var descriptor = CreateDescriptor("forecasthub-1", this.root, this.root, null);
        descriptor.Urls = [];
        var sut = new HostRuntimeInfo { Descriptor = descriptor };

        // Act
        var result = sut.GetPrimaryUrl();

        // Assert
        result.ShouldBe("https://localhost:5111");
    }

    [Fact]
    public void GetUrls_WhenAddressesUseWildcards_NormalizesAndOrdersHttpsFirst()
    {
        // Arrange
        string[] addresses = ["http://[::]:5110", "https://+:5111/", "http://[::]:5110"];

        // Act
        var result = HostRuntimeDescriptorWriter.GetUrls(addresses, null);

        // Assert
        result.ShouldBe(["https://localhost:5111", "http://localhost:5110"]);
    }

    [Fact]
    public void GetUrls_WhenServerHasNotStarted_UsesInstanceUrls()
    {
        // Arrange
        var context = new DevAppInstanceContext
        {
            IsActive = true,
            HttpPort = 5110,
            HttpsPort = 5111
        };

        // Act
        var result = HostRuntimeDescriptorWriter.GetUrls([], context);

        // Assert
        result.ShouldBe(["https://localhost:5111", "http://localhost:5110"]);
    }

    [Fact]
    public void ResolveWorkspaceRoot_WhenLinkedWorktreeHasGitFile_ReturnsWorktreeRoot()
    {
        // Arrange
        var worktreeRoot = this.CreateDirectory("linked");
        File.WriteAllText(Path.Combine(worktreeRoot, ".git"), "gitdir: /elsewhere/.git/worktrees/linked");
        var nested = Path.Combine(worktreeRoot, "src", "App");
        Directory.CreateDirectory(nested);

        // Act
        var result = WorkspacePathUtilities.ResolveWorkspaceRoot(nested);

        // Assert
        result.ShouldBe(WorkspacePathUtilities.Normalize(worktreeRoot));
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(this.root, name);
        Directory.CreateDirectory(path);

        return path;
    }

    private static DateTimeOffset? CurrentProcessStartedAt()
    {
        using var process = Process.GetCurrentProcess();

        return new DateTimeOffset(process.StartTime);
    }

    private static void WriteDescriptor(string registryPath, HostRuntimeDescriptor descriptor)
        => File.WriteAllText(Path.Combine(registryPath, descriptor.RuntimeId + ".json"), JsonSerializer.Serialize(descriptor, CliJson.Options));

    private static HostRuntimeDescriptor CreateDescriptor(string runtimeId, string workspacePath, string worktreeRoot, DateTimeOffset? processStartedAt)
        => new()
        {
            RuntimeId = runtimeId,
            ApplicationName = "ForecastHub",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "ForecastHub", Version = "1.0.0" },
            Features =
            {
                ["mcp"] = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = runtimeId, Nonce = "nonce" }
            },
            DevAppInstance = new HostRuntimeDevAppInstanceDescriptor
            {
                AppId = "forecasthub",
                WorktreeId = Guid.NewGuid(),
                Slot = 1,
                WorktreeRoot = worktreeRoot,
                HttpUrl = "http://localhost:5110",
                HttpsUrl = "https://localhost:5111",
                ProcessStartedAt = processStartedAt
            }
        };
}
