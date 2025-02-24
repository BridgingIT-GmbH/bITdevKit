// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Identity;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class EntityPermissionProviderTests : IClassFixture<StubDbContextFixture>
{
    private readonly ILoggerFactory loggerFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IEntityPermissionProvider provider;
    private readonly StubDbContext dbContext;

    public EntityPermissionProviderTests(StubDbContextFixture fixture)
    {
        this.loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        var services = new ServiceCollection();
        services.AddSingleton(fixture.Context);
        services.AddSingleton(this.loggerFactory);

        services.AddAuthorization2(o =>
            o.WithEntityPermissions<StubDbContext>(e =>
            {
                e.AddEntity<PersonStub>();
            })
        );

        this.serviceProvider = services.BuildServiceProvider();
        this.dbContext = fixture.Context;
        this.provider = this.serviceProvider.GetRequiredService<IEntityPermissionProvider>();
    }

    [Fact]
    public async Task UserPermissions_ShouldGrantAccess()
    {
        // Arrange
        var person1 = new PersonStub
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Age = 30
        };
        var person2 = new PersonStub
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            Age = 28
        };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([person1, person2]);
        await this.dbContext.SaveChangesAsync();

        // Grant permission to person1
        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            person1.Id,
            Permission.For("Read"));

        // Act & Assert
        var hasPerson1Access = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            person1.Id,
            Permission.For("Read"));
        hasPerson1Access.ShouldBeTrue("Should have access to person1");

        var hasPerson2Access = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            person2.Id,
            Permission.For("Read"));
        hasPerson2Access.ShouldBeFalse("Should not have access to person2");
    }

    [Fact]
    public async Task WildcardPermissions_ShouldGrantAccessToAllPersons()
    {
        // Arrange
        var person1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Jane" };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([person1, person2]);
        await this.dbContext.SaveChangesAsync();

        // Grant wildcard permission
        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            null,
            Permission.Read);

        // Act & Assert
        var hasPermission = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            Permission.Read);
        hasPermission.ShouldBeTrue("Should have wildcard permission");

        var hasPerson1Access = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            person1.Id,
            Permission.Read);
        hasPerson1Access.ShouldBeTrue("Should have access to person1 through wildcard");

        var hasPerson2Access = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            person2.Id,
            Permission.Read);
        hasPerson2Access.ShouldBeTrue("Should have access to person2 through wildcard");
    }

    [Fact]
    public async Task RolePermissions_ShouldGrantAccessThroughRole()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        // Grant permission to role
        await this.provider.GrantRolePermissionAsync(
            "Admins",
            nameof(PersonStub),
            person.Id,
            Permission.Read);

        // Act & Assert
        var hasAccessWithRole = await this.provider.HasPermissionAsync(
            "user1",
            ["Admins"],
            nameof(PersonStub),
            person.Id,
            Permission.Read);
        hasAccessWithRole.ShouldBeTrue("Should have access through role");

        var hasAccessWithoutRole = await this.provider.HasPermissionAsync(
            "user1",
            [],
            nameof(PersonStub),
            person.Id,
            Permission.Read);
        hasAccessWithoutRole.ShouldBeFalse("Should not have access without role");
    }

    [Fact]
    public async Task RevokePermissions_ShouldRemoveAccess()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();
        var userId = DateTime.UtcNow.Ticks.ToString();

        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            person.Id,
            Permission.Read);

        // Verify initial access
        var hasInitialAccess = await this.provider.HasPermissionAsync(
            userId, [], nameof(PersonStub), person.Id, Permission.Read);
        hasInitialAccess.ShouldBeTrue("Should have initial access");

        // Act
        await this.provider.RevokeUserPermissionAsync(
            userId,
            nameof(PersonStub),
            person.Id,
            Permission.Read);

        // Assert
        var hasAccessAfterRevoke = await this.provider.HasPermissionAsync(
            userId, [], nameof(PersonStub), person.Id, Permission.Read);
        hasAccessAfterRevoke.ShouldBeFalse("Should not have access after revoke");
    }

    [Fact]
    public async Task CombinedPermissions_UserAndRole_ShouldWorkIndependently()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Editors_{DateTime.UtcNow.Ticks}";

        // Grant different permissions to user and role
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);

        // Act & Assert
        var hasReadWithoutRole = await this.provider.HasPermissionAsync(
            userId, [], nameof(PersonStub), person.Id, Permission.Read);
        hasReadWithoutRole.ShouldBeTrue("Should have read access through user permission");

        var hasWriteWithoutRole = await this.provider.HasPermissionAsync(
            userId, [], nameof(PersonStub), person.Id, Permission.Write);
        hasWriteWithoutRole.ShouldBeFalse("Should not have write access without role");

        var hasWriteWithRole = await this.provider.HasPermissionAsync(
            userId, [roleName], nameof(PersonStub), person.Id, Permission.Write);
        hasWriteWithRole.ShouldBeTrue("Should have write access through role");
    }

    [Fact]
    public async Task MultipleRoles_ShouldCombinePermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var ticks = DateTime.UtcNow.Ticks;
        var readersRole = $"Readers_{ticks}";
        var writersRole = $"Writers_{ticks}";

        // Grant different permissions to different roles
        await this.provider.GrantRolePermissionAsync(readersRole, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(writersRole, nameof(PersonStub), person.Id, Permission.Write);

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Act & Assert
        var hasReadWithReaderRole = await this.provider.HasPermissionAsync(
            userId, [readersRole], nameof(PersonStub), person.Id, Permission.Read);
        hasReadWithReaderRole.ShouldBeTrue("Should have read access through Readers role");

        var hasWriteWithWriterRole = await this.provider.HasPermissionAsync(
            userId, [writersRole], nameof(PersonStub), person.Id, Permission.Write);
        hasWriteWithWriterRole.ShouldBeTrue("Should have write access through Writers role");

        var hasBothWithBothRoles = await this.provider.HasPermissionAsync(
            userId, [readersRole, writersRole], nameof(PersonStub), person.Id, Permission.Write);
        hasBothWithBothRoles.ShouldBeTrue("Should have both permissions with both roles");
    }

    [Fact]
    public async Task RevokeRolePermission_ShouldNotAffectUserPermission()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Readers_{DateTime.UtcNow.Ticks}";

        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Read);

        // Act
        await this.provider.RevokeRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Read);

        // Assert
        var hasAccessWithoutRole = await this.provider.HasPermissionAsync(
            userId, [], nameof(PersonStub), person.Id, Permission.Read);
        hasAccessWithoutRole.ShouldBeTrue("Should still have access through user permission");
    }

    [Fact]
    public async Task TypePermissions_ShouldCheckWildcardPermissions()
    {
        // Arrange
        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Readers_{DateTime.UtcNow.Ticks}";

        // Grant type-wide permission to user
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            null,
            Permission.Read);

        // Act & Assert
        var hasTypePermission = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            Permission.Read);
        hasTypePermission.ShouldBeTrue("Should have type-wide permission");

        // Grant type-wide permission to role
        await this.provider.GrantRolePermissionAsync(
            roleName,
            nameof(PersonStub),
            null,
            Permission.Write);

        var hasRoleTypePermission = await this.provider.HasPermissionAsync(
            userId,
            [roleName],
            nameof(PersonStub),
            Permission.Write);
        hasRoleTypePermission.ShouldBeTrue("Should have type-wide permission through role");
    }

    [Fact]
    public async Task TypePermissions_ShouldNotGrantAccessIfOnlyEntitySpecificExists()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Readers_{DateTime.UtcNow.Ticks}";

        // Grant entity-specific permissions
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            person.Id,
            Permission.Read);

        await this.provider.GrantRolePermissionAsync(
            roleName,
            nameof(PersonStub),
            person.Id,
            Permission.Write);

        // Act & Assert
        var hasTypePermission = await this.provider.HasPermissionAsync(
            userId,
            [],
            nameof(PersonStub),
            Permission.Read);
        hasTypePermission.ShouldBeFalse("Should not have type-wide permission when only entity-specific exists");

        var hasRoleTypePermission = await this.provider.HasPermissionAsync(
            userId,
            [roleName],
            nameof(PersonStub),
            Permission.Write);
        hasRoleTypePermission.ShouldBeFalse("Should not have type-wide permission through role when only entity-specific exists");
    }

    [Fact]
    public async Task TypePermissions_ShouldWorkWithMultipleRoles()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var readersRole = $"Readers_{ticks}";
        var writersRole = $"Writers_{ticks}";

        // Grant type-wide permissions to different roles
        await this.provider.GrantRolePermissionAsync(
            readersRole,
            nameof(PersonStub),
            null,
            Permission.Read);

        await this.provider.GrantRolePermissionAsync(
            writersRole,
            nameof(PersonStub),
            null,
            Permission.Write);

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Act & Assert
        var hasReadWithReaderRole = await this.provider.HasPermissionAsync(
            userId,
            [readersRole],
            nameof(PersonStub),
            Permission.Read);
        hasReadWithReaderRole.ShouldBeTrue("Should have type-wide read access through Readers role");

        var hasWriteWithWriterRole = await this.provider.HasPermissionAsync(
            userId,
            [writersRole],
            nameof(PersonStub),
            Permission.Write);
        hasWriteWithWriterRole.ShouldBeTrue("Should have type-wide write access through Writers role");

        var hasBothWithBothRoles = await this.provider.HasPermissionAsync(
            userId,
            [readersRole, writersRole],
            nameof(PersonStub),
            Permission.Write);
        hasBothWithBothRoles.ShouldBeTrue("Should have both type-wide permissions with both roles");
    }

    [Fact]
    public async Task GetUserPermissions_ShouldReturnDirectAndWildcardPermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant specific and wildcard permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Write);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);

        // Act
        var permissions = await this.provider.GetUserPermissionsAsync(userId, nameof(PersonStub), person.Id);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(Permission.Read);
        permissions.ShouldContain(Permission.Write);
        permissions.ShouldContain(Permission.List);
    }

    [Fact]
    public async Task GetUserPermissions_ShouldReturnEmptyForNonExistingPermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Act
        var permissions = await this.provider.GetUserPermissionsAsync(userId, nameof(PersonStub), person.Id);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetUserPermissions_ShouldNotIncludeRolePermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Readers_{DateTime.UtcNow.Ticks}";

        // Grant user and role permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);

        // Act
        var permissions = await this.provider.GetUserPermissionsAsync(userId, nameof(PersonStub), person.Id);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(1);
        permissions.ShouldContain(Permission.Read);
        permissions.ShouldNotContain(Permission.Write);
    }

    [Fact]
    public async Task GetRolePermissions_ShouldReturnDirectAndWildcardPermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var roleName = $"Admins_{DateTime.UtcNow.Ticks}";

        // Grant specific and wildcard permissions to role
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), null, Permission.List);

        // Act
        var permissions = await this.provider.GetRolePermissionsAsync(roleName, nameof(PersonStub), person.Id);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(Permission.Read);
        permissions.ShouldContain(Permission.Write);
        permissions.ShouldContain(Permission.List);
    }

    [Fact]
    public async Task GetRolePermissions_ShouldReturnEmptyForNonExistingRole()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var nonExistingRole = $"NonExisting_{DateTime.UtcNow.Ticks}";

        // Act
        var permissions = await this.provider.GetRolePermissionsAsync(nonExistingRole, nameof(PersonStub), person.Id);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetRolePermissions_ShouldNotIncludeUserPermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Readers_{DateTime.UtcNow.Ticks}";

        // Grant user and role permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);

        // Act
        var permissions = await this.provider.GetRolePermissionsAsync(roleName, nameof(PersonStub), person.Id);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(1);
        permissions.ShouldContain(Permission.Write);
        permissions.ShouldNotContain(Permission.Read);
    }
}