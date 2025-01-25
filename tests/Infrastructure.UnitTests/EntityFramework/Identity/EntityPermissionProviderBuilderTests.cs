// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Identity;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class EntityPermissionProviderBuilderTests
{
    private readonly ILoggerFactory loggerFactory;
    private readonly IEntityPermissionProvider provider;

    public EntityPermissionProviderBuilderTests()
    {
        // Setup
        this.loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        // Setup service provider with InMemory DbContext
        var services = new ServiceCollection();
        services.AddDbContext<StubDbContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString("N")), contextLifetime: ServiceLifetime.Singleton);

        this.provider = new EntityFrameworkPermissionProvider<StubDbContext>(this.loggerFactory, services.BuildServiceProvider());
    }

    [Fact]
    public async Task Builder_WithUserPermissions_ShouldGrantPermissions()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForUser(userId)
                .WithPermission(entityType, entityId, Permission.Read)
                .WithPermission(entityType, entityId, Permission.Update)
            .Build();

        // Assert
        var hasRead = await sut.HasPermissionAsync(
            userId,
            [],
            entityType,
            entityId,
            Permission.Read);

        var hasUpdate = await sut.HasPermissionAsync(
            userId,
            [],
            entityType,
            entityId,
            Permission.Update);

        hasRead.ShouldBeTrue();
        hasUpdate.ShouldBeTrue();
    }

    [Fact]
    public async Task Builder_WithWildcardPermission_ShouldGrantWildcardPermission()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123"; // Any ID should work with wildcard

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForUser(userId)
                .WithPermission(entityType, Permission.Read)
            .Build();

        // Assert
        var hasPermission = await sut.HasPermissionAsync(
            userId,
            [],
            entityType,
            entityId,
            Permission.Read);

        hasPermission.ShouldBeTrue();
    }

    [Fact]
    public async Task Builder_WithRolePermissions_ShouldGrantGroupPermissions()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForRole(Role.Administrators)
                .WithPermission(entityType, entityId, Permission.Read)
            .Build();

        // Assert
        var hasPermission = await sut.HasPermissionAsync(
            userId,
            [Role.Administrators],
            entityType,
            entityId,
            Permission.Read);

        hasPermission.ShouldBeTrue();
    }

    [Fact]
    public async Task Builder_WithMultipleUsersAndRoles_ShouldGrantAllPermissions()
    {
        // Arrange
        const string user1 = "user1";
        const string user2 = "user2";
        string role1 = Role.For("Role1");
        string role2 = Role.For("Role2");
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForUser(user1)
                .WithPermission(entityType, entityId, Permission.For("Read"))
            .ForUser(user2)
                .WithPermission(entityType, entityId, Permission.Update)
            .ForRole(role1)
                .WithPermission(entityType, Permission.Delete)
            .ForRole(role2)
                .WithPermission(entityType, entityId, Permission.Create)
            .Build();

        // Assert
        var user1HasRead = await sut.HasPermissionAsync(user1, [], entityType, entityId, Permission.Read);
        var user2HasUpdate = await sut.HasPermissionAsync(user2, [], entityType, entityId, Permission.Update);
        var user1WithRole1HasDelete = await sut.HasPermissionAsync(user1, [role1], entityType, entityId, Permission.Delete);
        var user2WithRole2HasCreate = await sut.HasPermissionAsync(user2, [role2], entityType, entityId, Permission.Create);

        user1HasRead.ShouldBeTrue();
        user2HasUpdate.ShouldBeTrue();
        user1WithRole1HasDelete.ShouldBeTrue();
        user2WithRole2HasCreate.ShouldBeTrue();
    }

    [Fact]
    public async Task Builder_WithoutPermission_ShouldNotGrantAccess()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForUser(userId)
                .WithPermission(entityType, entityId, Permission.Read)
            .Build();

        // Assert
        var hasUpdate = await sut.HasPermissionAsync(
            userId,
            [],
            entityType,
            entityId,
            Permission.Update); // different permission requested

        hasUpdate.ShouldBeFalse();
    }

    [Fact]
    public async Task Builder_WithWildcardAndSpecificPermissions_ShouldPreferSpecific()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForUser(userId)
                .WithPermission(entityType, Permission.Read)
                .WithPermission(entityType, entityId, Permission.Write)
            .Build();

        // Assert
        var hasWildcardRead = await sut.HasPermissionAsync(
            userId, [], entityType, "differentId", Permission.Read);
        var hasSpecificWrite = await sut.HasPermissionAsync(
            userId, [], entityType, entityId, Permission.Write);

        hasWildcardRead.ShouldBeTrue("Wildcard permission should grant access to any entity");
        hasSpecificWrite.ShouldBeTrue("Specific permission should be granted");
    }

    [Fact]
    public async Task Builder_WithRoleWildcardAndUserSpecific_ShouldGrantBoth()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForRole(Role.Administrators)
                .WithPermission(entityType, Permission.Read)
            .ForUser(userId)
                .WithPermission(entityType, entityId, Permission.Write)
            .Build();

        // Assert
        var hasRoleWildcardRead = await sut.HasPermissionAsync(
            userId, [Role.Administrators], entityType, "anyId", Permission.Read);
        var hasUserSpecificWrite = await sut.HasPermissionAsync(
            userId, [], entityType, entityId, Permission.Write);
        var differentUserNoAccess = await sut.HasPermissionAsync(
            "otherUser", [], entityType, entityId, Permission.Write);

        hasRoleWildcardRead.ShouldBeTrue("Role wildcard should grant read access");
        hasUserSpecificWrite.ShouldBeTrue("User specific permission should be granted");
        differentUserNoAccess.ShouldBeFalse("Different user should not have access");
    }

    [Fact]
    public async Task Builder_WithMultipleRoles_ShouldCombinePermissions()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForRole(Role.Readers)
                .WithPermission(entityType, entityId, Permission.Read)
            .ForRole(Role.Writers)
                .WithPermission(entityType, entityId, Permission.Write)
            .Build();

        // Assert
        var hasReadWithReaderRole = await sut.HasPermissionAsync(
            userId, [Role.Readers], entityType, entityId, Permission.Read);
        var hasWriteWithWriterRole = await sut.HasPermissionAsync(
            userId, [Role.Writers], entityType, entityId, Permission.Write);
        var hasBothWithBothRoles = await sut.HasPermissionAsync(
            userId, [Role.Readers, Role.Writers], entityType, entityId, Permission.Write);
        var hasNoneWithNoRoles = await sut.HasPermissionAsync(
            userId, [], entityType, entityId, Permission.Write);

        hasReadWithReaderRole.ShouldBeTrue("Reader role should grant read access");
        hasWriteWithWriterRole.ShouldBeTrue("Writer role should grant write access");
        hasBothWithBothRoles.ShouldBeTrue("Both roles should grant both permissions");
        hasNoneWithNoRoles.ShouldBeFalse("No roles should grant no access");
    }

    [Fact]
    public async Task Builder_WithDifferentEntityTypes_ShouldNotCrossGrant()
    {
        // Arrange
        const string userId = "user1";
        const string type1 = "EntityType1";
        const string type2 = "EntityType2";
        const string entityId = "123";

        // Act
        var sut = new EntityPermissionProviderBuilder(this.provider)
            .ForUser(userId)
                .WithPermission(type1, entityId, Permission.Read)
                .WithPermission(type2, Permission.Write)
            .Build();

        // Assert
        var hasType1Read = await sut.HasPermissionAsync(
            userId, [], type1, entityId, Permission.Read);
        var hasType1Write = await sut.HasPermissionAsync(
            userId, [], type1, entityId, Permission.Write);
        var hasType2Read = await sut.HasPermissionAsync(
            userId, [], type2, entityId, Permission.Read);
        var hasType2Write = await sut.HasPermissionAsync(
            userId, [], type2, entityId, Permission.Write);

        hasType1Read.ShouldBeTrue("Should have specific permission on type1");
        hasType1Write.ShouldBeFalse("Should not have write permission on type1");
        hasType2Read.ShouldBeFalse("Should not have read permission on type2");
        hasType2Write.ShouldBeTrue("Should have wildcard write permission on type2");
    }

    [Fact]
    public async Task Builder_WithSubsequentGrants_ShouldKeepAllPermissions()
    {
        // Arrange
        const string userId = "user1";
        const string entityType = "TestEntity";
        const string entityId = "123";

        // Act
        var builder = new EntityPermissionProviderBuilder(this.provider);

        // First grant
        builder.ForUser(userId)
            .WithPermission(entityType, entityId, Permission.Read);

        // Second grant
        builder.ForUser(userId)
            .WithPermission(entityType, entityId, Permission.Write);

        var sut = builder.Build();

        // Assert
        var hasRead = await sut.HasPermissionAsync(
            userId, [], entityType, entityId, Permission.Read);
        var hasWrite = await sut.HasPermissionAsync(
            userId, [], entityType, entityId, Permission.Write);

        hasRead.ShouldBeTrue("First granted permission should be kept");
        hasWrite.ShouldBeTrue("Second granted permission should be added");
    }
}