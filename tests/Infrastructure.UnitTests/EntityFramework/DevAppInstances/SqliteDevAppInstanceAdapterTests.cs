// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.DevAppInstances;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

public class SqliteDevAppInstanceAdapterTests : IDisposable
{
    private const string CoreKey = "Modules:Core:ConnectionStrings:Default";

    private readonly FakeGitRepository repository = new();

    public void Dispose() => this.repository.Dispose();

    [Fact]
    public void Resolve_LinkedWorktree_InsertsSlotBeforeExtension()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var file = Path.Combine(worktree, "data", "forecasthub.db");
        var settings = new Dictionary<string, string> { [CoreKey] = $"Data Source={file};Cache=Shared" };

        // Act
        var sut = this.Resolve(worktree, settings);

        // Assert
        var value = new SqliteConnectionStringBuilder(sut.ManagedValues[CoreKey]);
        value.DataSource.ShouldBe(Path.Combine(worktree, "data", "forecasthub_w01.db"));
        value.Cache.ShouldBe(SqliteCacheMode.Shared);
        var resource = sut.Resources.Single();
        resource.Kind.ShouldBe("sqlite");
        resource.ResourceId.ShouldBe("forecasthub");
        resource.Target.ShouldBe(Path.GetFullPath(value.DataSource));
        resource.ServerIdentity.ShouldBeNull();
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=forecasthub;Mode=Memory;Cache=Shared")]
    [InlineData("Data Source=file:forecasthub.db?mode=ro")]
    [InlineData("Server=localhost;Database=forecasthub")]
    public void Resolve_InMemoryUriOrForeignValue_IsNotDiscovered(string value)
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");

        // Act
        var sut = this.Resolve(worktree, new Dictionary<string, string> { [CoreKey] = value });

        // Assert
        sut.Resources.ShouldBeEmpty();
        sut.ManagedValues.ContainsKey(CoreKey).ShouldBeFalse();
    }

    [Fact]
    public async Task ResetAsync_InstanceFile_DeletesFileAndSidecars()
    {
        // Arrange
        var directory = Directory.CreateDirectory(Path.Combine(this.repository.Root, "reset")).FullName;
        var file = Path.Combine(directory, "forecasthub_w01.db");
        foreach (var path in new[] { file, file + "-wal", file + "-shm" })
        {
            await File.WriteAllTextAsync(path, "x");
        }

        var target = new DevAppInstanceInventoryResource("forecasthub", "sqlite", file, null, [CoreKey], false);

        // Act
        var result = await SqliteDevAppInstanceAdapter.Instance.ResetAsync(
            [target], new DevAppInstanceContext { Slot = 1 }, new ConfigurationBuilder().Build(), CancellationToken.None);

        // Assert
        result.Completed.ShouldBe([file]);
        File.Exists(file).ShouldBeFalse();
        File.Exists(file + "-wal").ShouldBeFalse();
        File.Exists(file + "-shm").ShouldBeFalse();
    }

    [Fact]
    public void ValidateOrphanTarget_Targets_ReturnExpectedRefusals()
    {
        // Arrange
        var directory = Path.Combine(this.repository.Root, "shared-data");
        var settings = new Dictionary<string, string> { [CoreKey] = $"Data Source={Path.Combine(directory, "forecasthub.db")}" };
        this.Resolve(this.repository.AddWorktree("wt1"), settings);
        var second = this.Resolve(this.repository.AddWorktree("wt2"), settings);
        var orphan = DevAppInstanceResolver.ResolveOrphan(
            this.repository.Request(this.repository.MainRoot, settings: settings, databaseAdapters: [SqliteDevAppInstanceAdapter.Instance]),
            second.WorktreeId.Value);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var sut = SqliteDevAppInstanceAdapter.Instance;
        DevAppInstanceInventoryResource Target(string path) => new("forecasthub", "sqlite", path, null, [CoreKey], false);

        // Act & Assert
        sut.ValidateOrphanTarget(Target(Path.Combine(directory, "forecasthub_w02.db")), orphan.Context, configuration).ShouldBeNull();
        sut.ValidateOrphanTarget(Target(Path.Combine(directory, "forecasthub_w01.db")), orphan.Context, configuration)
            .ShouldContain("does not match the generated name");
        sut.ValidateOrphanTarget(Target(Path.Combine(this.repository.Root, "elsewhere", "forecasthub_w02.db")), orphan.Context, configuration)
            .ShouldContain("outside the configured directory");
    }

    private DevAppInstanceContext Resolve(string contentRoot, IDictionary<string, string> settings)
    {
        var builder = new DevAppInstanceBuilder().WithSqliteDatabases();

        return DevAppInstanceResolver.Resolve(this.repository.Request(
            contentRoot, settings: settings, resources: builder.Resources, databaseAdapters: builder.DatabaseAdapters, sharedKeys: builder.SharedKeys));
    }
}
