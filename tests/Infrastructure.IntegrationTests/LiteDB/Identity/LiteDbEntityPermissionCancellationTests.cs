// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Identity;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using PermissionNode = LiteDbEntityPermissionTests.PermissionNode;

[IntegrationTest("Infrastructure")]
public class LiteDbEntityPermissionCancellationTests
{
    [Theory]
    [InlineData("GrantUser")]
    [InlineData("GrantRole")]
    [InlineData("RevokeUser")]
    [InlineData("RevokeRole")]
    [InlineData("RevokeUsers")]
    [InlineData("RevokeRoles")]
    public async Task MutationAsync_CallerCancelsAfterNativeWrite_CompletesCacheEvictionAndPreservesCommittedPermissions(string operation)
    {
        // Arrange
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-permission-cancel-");
        try
        {
            using var memory = new MemoryCache(new MemoryCacheOptions());
            using var cancellation = new CancellationTokenSource();
            var evictionTokens = new List<CancellationToken>();
            Action cancelAfterWrite = null;
            var cache = Substitute.For<ICacheProvider>();
            cache.Get<EntityPermissionCacheEntry>(Arg.Any<string>()).Returns(call => memory.Get<EntityPermissionCacheEntry>(call.Arg<string>()));
            cache.TryGet(Arg.Any<string>(), out Arg.Any<EntityPermissionCacheEntry>()).Returns(call =>
            {
                var found = memory.TryGetValue(call.ArgAt<string>(0), out EntityPermissionCacheEntry entry);
                call[1] = entry;
                return found;
            });
            cache.When(provider => provider.Set(Arg.Any<string>(), Arg.Any<EntityPermissionCacheEntry>(), Arg.Any<TimeSpan?>(), Arg.Any<DateTimeOffset?>()))
                .Do(call => memory.Set(call.ArgAt<string>(0), call.ArgAt<EntityPermissionCacheEntry>(1)));
            cache.RemoveStartsWithAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                // This callback is reached only after the native command has succeeded.
                var action = cancelAfterWrite;
                cancelAfterWrite = null;
                action?.Invoke();
                var token = call.Arg<CancellationToken>();
                evictionTokens.Add(token);
                token.ThrowIfCancellationRequested();
                foreach (var key in memory.Keys.OfType<string>().Where(key => key.StartsWith(call.Arg<string>(), StringComparison.Ordinal)).ToArray())
                {
                    memory.Remove(key);
                }

                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(cache);
            services.AddLiteDbDatabase(options => options.ConnectionString = Path.Combine(directory.FullName, "permissions.db"));
            services.AddLiteDbEntityPermissions(options => { options.EnableCaching = true; options.AddEntity<PermissionNode>(); });
            await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await using var scope = host.CreateAsyncScope();
            var sut = scope.ServiceProvider.GetRequiredService<IEntityPermissionProvider>();
            var evaluator = scope.ServiceProvider.GetRequiredService<IEntityPermissionEvaluator<PermissionNode>>();
            var entity = new PermissionNode { Id = Guid.NewGuid() };
            var grants = operation.StartsWith("Grant", StringComparison.Ordinal);
            var role = operation.Contains("Role", StringComparison.Ordinal);
            var roles = role ? new[] { "editors" } : Array.Empty<string>();
            if (!grants)
            {
                if (role) { await sut.GrantRolePermissionAsync<PermissionNode>("editors", entity.Id, "Read"); }
                else { await sut.GrantUserPermissionAsync<PermissionNode>("user", entity.Id, "Read"); }

                (await evaluator.HasPermissionAsync("user", roles, entity, "Read")).ShouldBeTrue();
                memory.Count.ShouldBeGreaterThan(0);
            }

            evictionTokens.Clear();
            cancelAfterWrite = cancellation.Cancel;

            // Act
            await (operation switch
            {
                "GrantUser" => sut.GrantUserPermissionAsync<PermissionNode>("user", entity.Id, "Read", cancellation.Token),
                "GrantRole" => sut.GrantRolePermissionAsync<PermissionNode>("editors", entity.Id, "Read", cancellation.Token),
                "RevokeUser" => sut.RevokeUserPermissionAsync<PermissionNode>("user", entity.Id, "Read", cancellation.Token),
                "RevokeRole" => sut.RevokeRolePermissionAsync<PermissionNode>("editors", entity.Id, "Read", cancellation.Token),
                "RevokeUsers" => sut.RevokeUserPermissionsAsync("user", cancellation.Token),
                "RevokeRoles" => sut.RevokeRolePermissionsAsync("editors", cancellation.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            });

            // Assert
            cancellation.IsCancellationRequested.ShouldBeTrue();
            evictionTokens.Count.ShouldBe(1);
            evictionTokens[0].CanBeCanceled.ShouldBeFalse();
            (await sut.HasPermissionAsync<PermissionNode>("user", roles, entity.Id, "Read")).ShouldBe(grants);
            (await evaluator.HasPermissionAsync("user", roles, entity, "Read")).ShouldBe(grants);
        }
        finally { directory.Delete(true); }
    }
}
