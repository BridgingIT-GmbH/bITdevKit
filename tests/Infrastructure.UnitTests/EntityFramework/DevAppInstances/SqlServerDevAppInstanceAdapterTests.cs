// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.DevAppInstances;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public class SqlServerDevAppInstanceAdapterTests : IDisposable
{
    private const string CoreKey = "Modules:Core:ConnectionStrings:Default";
    private const string SinkKey = "Serilog:WriteTo:3:Args:connectionString";
    private const string Baseline = "Server=127.0.0.1,14333;Database=forecasthub_core;User Id=sa;Password=secret;TrustServerCertificate=True";

    private readonly FakeGitRepository repository = new();

    public void Dispose() => this.repository.Dispose();

    [Fact]
    public void Resolve_LinkedWorktree_RewritesAllKeysOfSameDatabaseToInstanceDatabase()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var settings = new Dictionary<string, string> { [CoreKey] = Baseline, [SinkKey] = Baseline };

        // Act
        var sut = this.Resolve(worktree, settings, new DevAppInstanceBuilder().WithSqlServerDatabases());

        // Assert
        sut.IsActive.ShouldBeTrue();
        var core = new SqlConnectionStringBuilder(sut.ManagedValues[CoreKey]);
        core.InitialCatalog.ShouldBe("forecasthub_core_w01");
        core.Password.ShouldBe("secret");
        core.DataSource.ShouldBe("127.0.0.1,14333");
        new SqlConnectionStringBuilder(sut.ManagedValues[SinkKey]).InitialCatalog.ShouldBe("forecasthub_core_w01");
        var resource = sut.Resources.Single();
        resource.Target.ShouldBe("forecasthub_core_w01");
        resource.ResourceId.ShouldBe("forecasthub_core");
        resource.SourceKeys.ShouldBe([CoreKey, SinkKey], ignoreOrder: true);
    }

    [Fact]
    public void Resolve_SharedConnectionString_LeavesValueUnmanaged()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var settings = new Dictionary<string, string>
        {
            [CoreKey] = Baseline,
            ["ConnectionStrings:Reporting"] = Baseline.Replace("forecasthub_core", "reporting"),
        };
        var builder = new DevAppInstanceBuilder().WithSqlServerDatabases().ShareConnectionString("ConnectionStrings:Reporting");

        // Act
        var sut = this.Resolve(worktree, settings, builder);

        // Assert
        sut.ManagedValues.ContainsKey("ConnectionStrings:Reporting").ShouldBeFalse();
        sut.ManagedValues.ContainsKey(CoreKey).ShouldBeTrue();
    }

    [Fact]
    public void Resolve_KeysWithoutConnectionStringInPath_AreNotDiscovered()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var settings = new Dictionary<string, string>
        {
            [CoreKey] = Baseline,
            ["ConnectionStrings:Audit"] = Baseline.Replace("forecasthub_core", "forecasthub_audit"),
            ["Features:Greeting"] = "hello",
            ["ConnectionStrings:NoDatabase"] = "Server=127.0.0.1,14333;User Id=sa",
        };

        // Act
        var sut = this.Resolve(worktree, settings, new DevAppInstanceBuilder().WithSqlServerDatabases());

        // Assert
        sut.Resources.Select(r => r.Target).ShouldBe(["forecasthub_audit_w01", "forecasthub_core_w01"], ignoreOrder: true);
        sut.ManagedValues.Keys.Where(k => k.Contains("Connection", StringComparison.OrdinalIgnoreCase)).ShouldBe(["ConnectionStrings:Audit", CoreKey], ignoreOrder: true);
    }

    [Fact]
    public void Resolve_OrdinaryCheckout_ManagesNothing()
    {
        // Arrange
        var settings = new Dictionary<string, string> { [CoreKey] = Baseline };

        // Act
        var sut = this.Resolve(this.repository.MainRoot, settings, new DevAppInstanceBuilder().WithSqlServerDatabases());

        // Assert
        sut.IsActive.ShouldBeFalse();
        sut.ManagedValues.ShouldBeEmpty();
    }

    [Fact]
    public void FindOrdinaryReferences_UndeclaredKeyWithSameDatabase_IsReported()
    {
        // Arrange
        var sut = SqlServerDevAppInstanceAdapter.Instance;
        var plan = new DevAppInstanceResourcePlan("forecasthub_core", sut.Kind, "forecasthub_core_w01", "127.0.0.1,14333", [CoreKey], false, new Dictionary<string, string>());
        var baseline = new Dictionary<string, string> { [CoreKey] = Baseline };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            [CoreKey] = "managed",
            ["HealthChecks:Sql"] = Baseline.Replace("User Id=sa", "User Id=reader"),
            ["Other"] = "Server=127.0.0.1,14333;Database=unrelated",
            ["NotSql"] = "hello",
        }).Build();

        // Act
        var result = sut.FindOrdinaryReferences([plan], baseline, configuration, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CoreKey });

        // Assert
        result.ShouldBe(["HealthChecks:Sql"]);
    }

    [Fact]
    public void ResolveDesignTime_OrdinaryCheckout_ReturnsUnchanged()
    {
        // Act
        var result = SqlServerDevAppInstanceAdapter.Instance.ResolveDesignTime(Baseline, CoreKey, this.repository.MainRoot);

        // Assert
        result.ShouldBe(Baseline);
    }

    [Fact]
    public void ResolveDesignTime_LinkedWorktreeWithoutInventory_ThrowsAssignmentInvalid()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => SqlServerDevAppInstanceAdapter.Instance.ResolveDesignTime(Baseline, CoreKey, worktree))
            .Code.ShouldBe(DevAppInstanceErrorCodes.AssignmentInvalid);
    }

    [Fact]
    public void ResolveDesignTime_LinkedWorktreeAfterStart_UsesInstanceDatabase()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var project = Directory.CreateDirectory(Path.Combine(worktree, "src", "Api")).FullName;
        this.Resolve(worktree, new Dictionary<string, string> { [CoreKey] = Baseline }, new DevAppInstanceBuilder().WithSqlServerDatabases());

        // Act
        var result = SqlServerDevAppInstanceAdapter.Instance.ResolveDesignTime(Baseline, CoreKey, project);

        // Assert
        new SqlConnectionStringBuilder(result).InitialCatalog.ShouldBe("forecasthub_core_w01");
    }

    [Fact]
    public void ResolveDesignTime_ModeDisabled_ReturnsUnchanged()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [DevAppInstanceResolver.ModeKey] = "Disabled" }).Build();

        // Act
        var result = SqlServerDevAppInstanceAdapter.Instance.ResolveDesignTime(Baseline, CoreKey, worktree, configuration);

        // Assert
        result.ShouldBe(Baseline);
    }

    [Theory]
    [InlineData("forecasthub_core_w02", "127.0.0.1,14333", false, null)]
    [InlineData("forecasthub_core_w02", "127.0.0.1,14333", true, "Shared")]
    [InlineData("forecasthub_core_w01", "127.0.0.1,14333", false, "does not match the generated name")]
    [InlineData("forecasthub_core_w02", "otherhost", false, "configured server does not match")]
    public void ValidateOrphanTarget_Target_ReturnsExpectedRefusal(string database, string server, bool shared, string expected)
    {
        // Arrange
        var orphan = this.ResolveOrphanOfSecondWorktree();
        var target = new DevAppInstanceInventoryResource("forecasthub_core", "sqlserver", database, server, [CoreKey], shared);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { [CoreKey] = Baseline }).Build();

        // Act
        var result = SqlServerDevAppInstanceAdapter.Instance.ValidateOrphanTarget(target, orphan.Context, configuration);

        // Assert
        if (expected is null)
        {
            result.ShouldBeNull();
        }
        else
        {
            result.ShouldContain(expected);
        }
    }

    [Fact]
    public void ValidateOrphanTarget_HostOwnDatabase_IsRefused()
    {
        // Arrange
        var orphan = this.ResolveOrphanOfSecondWorktree();
        var target = new DevAppInstanceInventoryResource("forecasthub_core", "sqlserver", "forecasthub_core", "127.0.0.1,14333", [CoreKey], false);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { [CoreKey] = Baseline }).Build();

        // Act
        var result = SqlServerDevAppInstanceAdapter.Instance.ValidateOrphanTarget(target, orphan.Context, configuration);

        // Assert
        result.ShouldNotBeNull();
    }

    private DevAppInstanceContext Resolve(string contentRoot, IDictionary<string, string> settings, DevAppInstanceBuilder builder) =>
        DevAppInstanceResolver.Resolve(this.repository.Request(
            contentRoot,
            settings: settings,
            resources: builder.Resources,
            databaseAdapters: builder.DatabaseAdapters,
            sharedKeys: builder.SharedKeys));

    private DevAppInstanceOrphan ResolveOrphanOfSecondWorktree()
    {
        var settings = new Dictionary<string, string> { [CoreKey] = Baseline };
        var builder = new DevAppInstanceBuilder().WithSqlServerDatabases();
        this.Resolve(this.repository.AddWorktree("wt1"), settings, builder);
        var second = this.Resolve(this.repository.AddWorktree("wt2"), settings, builder);

        return DevAppInstanceResolver.ResolveOrphan(
            this.repository.Request(this.repository.MainRoot, settings: settings, databaseAdapters: builder.DatabaseAdapters),
            second.WorktreeId.Value);
    }
}
