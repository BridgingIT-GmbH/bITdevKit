// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Identity;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbEntityPermissionTests
{
    [Fact]
    public async Task Grants_AreIdempotent_ExactAndWildcard_AndScopedByPrincipalAndEntityType()
    {
        await using var host = new PermissionHost();
        var sut = host.Provider;
        await sut.GrantUserPermissionAsync<PermissionNode>("user", "A", "Read");
        await sut.GrantUserPermissionAsync<PermissionNode>("user", "A", "Read");
        await sut.GrantRolePermissionAsync<PermissionNode>("editors", null, "Edit");
        (await sut.GetPermissionsAsync<PermissionNode>("A")).Count.ShouldBe(2);
        (await sut.HasPermissionAsync<PermissionNode>("user", [], "A", "Read")).ShouldBeTrue();
        (await sut.HasPermissionAsync<PermissionNode>("user", [], "a", "Read")).ShouldBeFalse();
        (await sut.HasPermissionAsync<PermissionNode>("other", [], "A", "Read")).ShouldBeFalse();
        (await sut.HasPermissionAsync<PermissionNode>("user", ["editors"], "B", "Edit")).ShouldBeTrue();
        (await sut.HasPermissionAsync<PermissionNode>("user", ["editors"], "Edit")).ShouldBeTrue();
        (await sut.HasPermissionAsync("user", [], "OtherEntity", "A", "Read")).ShouldBeFalse();
        (await sut.GetEntityIdsWithPermissionAsync<PermissionNode>("user", [], "Read")).ShouldBe(["A"]);
        (await sut.GetEntityIdsWithPermissionAsync<PermissionNode>("user", ["editors"], "Edit")).ShouldBeEmpty();
        await sut.RevokeUserPermissionAsync<PermissionNode>("user", "A", "Read");
        (await sut.GetUserPermissionsAsync<PermissionNode>("user", "A")).ShouldBeEmpty();
        await sut.RevokeRolePermissionsAsync("editors");
        (await sut.GetRolesPermissionsAsync<PermissionNode>("B")).ShouldBeEmpty();
    }

    [Fact]
    public async Task UserAndRoleQueries_IncludeWildcardGrants_AndRevokeAllRetainsOtherPrincipals()
    {
        await using var host = new PermissionHost();
        var sut = host.Provider;
        await sut.GrantUserPermissionAsync<PermissionNode>("first", null, "Read");
        await sut.GrantUserPermissionAsync<PermissionNode>("first", "A", "Edit");
        await sut.GrantUserPermissionAsync<PermissionNode>("second", "A", "Read");
        await sut.GrantRolePermissionAsync<PermissionNode>("reviewers", "A", "Read");
        (await sut.GetUsersPermissionsAsync<PermissionNode>("A")).Count.ShouldBe(3);
        (await sut.GetUserPermissionsAsync<PermissionNode>("first", "A")).Order().ShouldBe(["Edit", "Read"]);
        (await sut.GetRolePermissionsAsync<PermissionNode>("reviewers", "A")).ShouldBe(["Read"]);
        await sut.RevokeUserPermissionsAsync("first");
        (await sut.GetPermissionsAsync<PermissionNode>(null)).Count.ShouldBe(2);
        await sut.RevokeRolePermissionAsync<PermissionNode>("reviewers", "A", "Read");
        (await sut.GetPermissionsAsync<PermissionNode>("A")).Single().UserId.ShouldBe("second");
    }

    [Fact]
    public async Task Hierarchy_UsesNativeEntities_HandlesCyclesAndMissingNodes_AndKeepsEvaluationInIdentity()
    {
        await using var host = new PermissionHost();
        var root = new PermissionNode { Id = Guid.NewGuid() };
        var parent = new PermissionNode { Id = Guid.NewGuid(), ParentId = root.Id };
        var child = new PermissionNode { Id = Guid.NewGuid(), ParentId = parent.Id };
        var repository = host.Scope.ServiceProvider.GetRequiredService<IGenericRepository<PermissionNode>>();
        await repository.InsertAsync(root); await repository.InsertAsync(parent); await repository.InsertAsync(child);
        (await host.Provider.GetHierarchyPathAsync<PermissionNode>(child.Id)).ShouldBe(new object[] { parent.Id, root.Id });
        (await host.Provider.GetHierarchyPathAsync<PermissionNode>(Guid.NewGuid())).ShouldBeEmpty();
        await host.Provider.GrantRolePermissionAsync<PermissionNode>("editors", root.Id, "Read");
        var evaluator = host.Scope.ServiceProvider.GetRequiredService<IEntityPermissionEvaluator<PermissionNode>>();
        (await evaluator.HasPermissionAsync("reader", ["editors"], child, "Read")).ShouldBeTrue();
        (await evaluator.HasPermissionAsync("reader", [], child, "Read")).ShouldBeFalse();
        root.ParentId = child.Id;
        await repository.UpdateAsync(root);
        (await host.Provider.GetHierarchyPathAsync<PermissionNode>(child.Id)).ShouldBe(new object[] { parent.Id, root.Id });
    }

    [Fact]
    public async Task CachedInheritedRoleGrants_AreEvictedWhenRevoked()
    {
        await using var host = new PermissionHost(enableCaching: true);
        var root = new PermissionNode { Id = Guid.NewGuid() };
        var child = new PermissionNode { Id = Guid.NewGuid(), ParentId = root.Id };
        var repository = host.Scope.ServiceProvider.GetRequiredService<IGenericRepository<PermissionNode>>();
        await repository.InsertAsync(root); await repository.InsertAsync(child);
        var evaluator = host.Scope.ServiceProvider.GetRequiredService<IEntityPermissionEvaluator<PermissionNode>>();
        await host.Provider.GrantRolePermissionAsync<PermissionNode>("editors", root.Id, "Read");
        (await evaluator.HasPermissionAsync("user", ["editors"], child, "Read")).ShouldBeTrue();
        await host.Provider.RevokeRolePermissionAsync<PermissionNode>("editors", root.Id, "Read");
        (await evaluator.HasPermissionAsync("user", ["editors"], child, "Read")).ShouldBeFalse();
    }

    [Fact]
    public async Task ScopedEntityTransaction_AllowsReentrantPermissionReads_AndRollsBackGrants()
    {
        await using var host = new PermissionHost();
        var session = host.Scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>("default");
        await session.BeginAsync();
        try
        {
            await host.Provider.GrantUserPermissionAsync<PermissionNode>("user", "id", "Read");
            (await host.Provider.HasPermissionAsync<PermissionNode>("user", [], "id", "Read")).ShouldBeTrue();
        }
        finally { await session.CompleteAsync(false); }

        (await host.Provider.HasPermissionAsync<PermissionNode>("user", [], "id", "Read")).ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluationInsideTransaction_DoesNotCacheARolledBackGrant()
    {
        await using var host = new PermissionHost(enableCaching: true);
        var session = host.Scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>("default");
        var evaluator = host.Scope.ServiceProvider.GetRequiredService<IEntityPermissionEvaluator<PermissionNode>>();
        var entity = new PermissionNode { Id = Guid.NewGuid() };
        (await evaluator.HasPermissionAsync("user", [], entity, "Read")).ShouldBeFalse();
        await session.BeginAsync();
        try
        {
            await host.Provider.GrantUserPermissionAsync<PermissionNode>("user", entity.Id, "Read");
            (await evaluator.HasPermissionAsync("user", [], entity, "Read")).ShouldBeTrue();
        }
        finally { await session.CompleteAsync(false); }

        (await evaluator.HasPermissionAsync("user", [], entity, "Read")).ShouldBeFalse();
    }

    [Fact]
    public async Task Grants_Reopen_AndCancellationDoesNotMutateStorage()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-permissions-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "permissions.db");
            await using (var host = new PermissionHost(file: file)) { await host.Provider.GrantUserPermissionAsync<PermissionNode>("user", "id", "Read"); }

            await using var reopened = new PermissionHost(file: file);
            var sut = reopened.Provider;
            (await sut.HasPermissionAsync<PermissionNode>("user", [], "id", "Read")).ShouldBeTrue();
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => sut.RevokeUserPermissionsAsync("user", canceled.Token));
            (await sut.HasPermissionAsync<PermissionNode>("user", [], "id", "Read")).ShouldBeTrue();
        }
        finally { directory.Delete(true); }
    }

    /// <summary>A fictional hierarchical entity persisted by the native repository.</summary>
    public sealed class PermissionNode : Entity<Guid>
    {
        /// <summary>Gets or sets the optional parent identity.</summary>
        public Guid? ParentId { get; set; }
    }

    private sealed class PermissionHost : IAsyncDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-litedb-permissions-");
        private readonly ServiceProvider services;
        public PermissionHost(string file = null, bool enableCaching = false)
        {
            global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLiteDbDatabase(o => o.ConnectionString = file ?? Path.Combine(this.directory.FullName, "permissions.db"));
            services.AddLiteDbRepository<PermissionNode>();
            services.AddLiteDbEntityPermissions(options => { options.AddHierarchicalEntity<PermissionNode>(entity => entity.ParentId); options.EnableCaching = enableCaching; });
            this.services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            this.Scope = this.services.CreateAsyncScope();
        }

        public AsyncServiceScope Scope { get; }

        public IEntityPermissionProvider Provider => this.Scope.ServiceProvider.GetRequiredService<IEntityPermissionProvider>();
        public async ValueTask DisposeAsync() { await this.Scope.DisposeAsync(); await this.services.DisposeAsync(); this.directory.Delete(true); }
    }
}
