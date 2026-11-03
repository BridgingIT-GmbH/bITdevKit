// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.DevAppInstances;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.Configuration;
using Npgsql;

public class PostgresDevAppInstanceAdapterTests : IDisposable
{
    private const string CoreKey = "Modules:Core:ConnectionStrings:Default";
    private const string Baseline = "Host=localhost;Port=15432;Database=forecasthub_core;Username=postgres;Password=secret";

    private readonly FakeGitRepository repository = new();

    public void Dispose() => this.repository.Dispose();

    [Fact]
    public void Resolve_LinkedWorktree_RewritesDatabaseAndKeepsOptions()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");

        // Act
        var sut = this.Resolve(worktree, new Dictionary<string, string> { [CoreKey] = Baseline });

        // Assert
        var value = new NpgsqlConnectionStringBuilder(sut.ManagedValues[CoreKey]);
        value.Database.ShouldBe("forecasthub_core_w01");
        value.Password.ShouldBe("secret");
        value.Port.ShouldBe(15432);
        var resource = sut.Resources.Single();
        resource.Kind.ShouldBe("postgres");
        resource.Target.ShouldBe("forecasthub_core_w01");
        resource.ServerIdentity.ShouldBe("localhost:15432");
    }

    [Fact]
    public void Resolve_DatabaseNameTooLong_ThrowsConfigurationConflict()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var settings = new Dictionary<string, string> { [CoreKey] = Baseline.Replace("forecasthub_core", new string('d', 60)) };

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => this.Resolve(worktree, settings))
            .Code.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
    }

    [Fact]
    public void ResolveDesignTime_LinkedWorktreeAfterStart_UsesInstanceDatabase()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        this.Resolve(worktree, new Dictionary<string, string> { [CoreKey] = Baseline });

        // Act
        var result = PostgresDevAppInstanceAdapter.Instance.ResolveDesignTime(Baseline, CoreKey, worktree);

        // Assert
        new NpgsqlConnectionStringBuilder(result).Database.ShouldBe("forecasthub_core_w01");
    }

    [Theory]
    [InlineData("forecasthub_core_w02", "localhost:15432", null)]
    [InlineData("forecasthub_core_w01", "localhost:15432", "does not match the generated name")]
    [InlineData("forecasthub_core_w02", "otherhost:5432", "configured server does not match")]
    public void ValidateOrphanTarget_Target_ReturnsExpectedRefusal(string database, string server, string expected)
    {
        // Arrange
        var settings = new Dictionary<string, string> { [CoreKey] = Baseline };
        this.Resolve(this.repository.AddWorktree("wt1"), settings);
        var second = this.Resolve(this.repository.AddWorktree("wt2"), settings);
        var orphan = DevAppInstanceResolver.ResolveOrphan(
            this.repository.Request(this.repository.MainRoot, settings: settings, databaseAdapters: [PostgresDevAppInstanceAdapter.Instance]),
            second.WorktreeId.Value);
        var target = new DevAppInstanceInventoryResource("forecasthub_core", "postgres", database, server, [CoreKey], false);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        // Act
        var result = PostgresDevAppInstanceAdapter.Instance.ValidateOrphanTarget(target, orphan.Context, configuration);

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

    private DevAppInstanceContext Resolve(string contentRoot, IDictionary<string, string> settings)
    {
        var builder = new DevAppInstanceBuilder().WithPostgresDatabases();

        return DevAppInstanceResolver.Resolve(this.repository.Request(
            contentRoot, settings: settings, resources: builder.Resources, databaseAdapters: builder.DatabaseAdapters, sharedKeys: builder.SharedKeys));
    }
}
