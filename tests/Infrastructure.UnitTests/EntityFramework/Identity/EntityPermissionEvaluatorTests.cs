// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Identity;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class NonHierarchicalEntityPermissionEvaluatorTests : IClassFixture<StubDbContextFixture>
{
    private readonly ILoggerFactory loggerFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IEntityPermissionProvider provider;
    private readonly IEntityPermissionEvaluator<PersonStub> evaluator;
    private readonly StubDbContext dbContext;

    public NonHierarchicalEntityPermissionEvaluatorTests(StubDbContextFixture fixture)
    {
        this.loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        var services = new ServiceCollection();
        services.AddSingleton(fixture.Context);
        services.AddSingleton(this.loggerFactory);

        services.AddEntityAuthorization(o =>
            o.WithEntityPermissions<StubDbContext>(e =>
            {
                e.AddEntity<PersonStub>();  // No hierarchy configuration
            })
        );

        this.serviceProvider = services.BuildServiceProvider();
        this.dbContext = fixture.Context;
        this.provider = this.serviceProvider.GetRequiredService<IEntityPermissionProvider>();
        this.evaluator = this.serviceProvider.GetRequiredService<IEntityPermissionEvaluator<PersonStub>>();
    }

    [Fact]
    public async Task HasPermission_DirectEntityPermissions_ShouldGrantAccess()
    {
        // Arrange
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager" };
        var employee1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee1", ManagerId = manager.Id };
        var employee2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee2", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([manager, employee1, employee2]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            employee1.Id,
            Permission.Read);

        // Act & Assert
        var hasEmployee1Access = await this.evaluator.HasPermissionAsync(
            userId,
            [],
            employee1,
            Permission.Read);
        hasEmployee1Access.ShouldBeTrue("Should have access to employee1");

        var hasEmployee2Access = await this.evaluator.HasPermissionAsync(
            userId,
            [],
            employee2,
            Permission.Read);
        hasEmployee2Access.ShouldBeFalse("Should not have access to employee2");
    }

    [Fact]
    public async Task HasPermission_TypeWidePermission_ShouldGrantAccessToAll()
    {
        // Arrange
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager" };
        var employee1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee1", ManagerId = manager.Id };
        var employee2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee2", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([manager, employee1, employee2]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            null, // type-wide permission
            Permission.Read);

        // Act & Assert
        // Check type-wide permission
        var hasTypePermission = await this.evaluator.HasPermissionAsync(
            userId,
            [],
            Permission.Read);
        hasTypePermission.ShouldBeTrue("Should have type-wide permission");

        // Check individual entities
        var hasManagerAccess = await this.evaluator.HasPermissionAsync(
            userId,
            [],
            manager,
            Permission.Read);
        hasManagerAccess.ShouldBeTrue("Should have access to manager through type-wide permission");

        var hasEmployee1Access = await this.evaluator.HasPermissionAsync(
            userId,
            [],
            employee1,
            Permission.Read);
        hasEmployee1Access.ShouldBeTrue("Should have access to employee1 through type-wide permission");

        var hasEmployee2Access = await this.evaluator.HasPermissionAsync(
            userId,
            [],
            employee2,
            Permission.Read);
        hasEmployee2Access.ShouldBeTrue("Should have access to employee2 through type-wide permission");
    }

    [Fact]
    public async Task GetPermissions_ShouldReturnAllPermissionSources()
    {
        // Arrange
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager" };
        var employee1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee1", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([manager, employee1]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Readers_{DateTime.UtcNow.Ticks}";

        // Grant different types of permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), employee1.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), employee1.Id, Permission.Write);

        // Act
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [roleName], employee1);

        // Assert
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(3);

        // Direct entity permission
        permissions.ShouldContain(p =>
            p.Permission == Permission.Read &&
            p.Source == "Direct");
        //p.EntityId == (object)employee1.Id);

        // Type-wide permission
        permissions.ShouldContain(p =>
            p.Permission == Permission.List &&
            p.Source == "Direct");
        //p.EntityId == null);

        // Role permission
        permissions.ShouldContain(p =>
            p.Permission == Permission.Write &&
            p.Source == $"Role:{roleName}");
        //p.EntityId == (object)employee1.Id);
    }

    [Fact]
    public async Task HasPermission_MultipleRoles_ShouldCombinePermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var ticks = DateTime.UtcNow.Ticks;
        var readersRole = $"Readers_{ticks}";
        var writersRole = $"Writers_{ticks}";
        var userId = ticks.ToString();

        // Grant different permissions to different roles
        await this.provider.GrantRolePermissionAsync(readersRole, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(writersRole, nameof(PersonStub), person.Id, Permission.Write);

        // Act & Assert
        var hasReadWithReaderRole = await this.evaluator.HasPermissionAsync(
            userId,
            [readersRole],
            person,
            Permission.Read);
        hasReadWithReaderRole.ShouldBeTrue("Should have read access through Readers role");

        var hasWriteWithWriterRole = await this.evaluator.HasPermissionAsync(
            userId,
            [writersRole],
            person,
            Permission.Write);
        hasWriteWithWriterRole.ShouldBeTrue("Should have write access through Writers role");

        // Check permissions with both roles
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [readersRole, writersRole], person);
        permissions.Count.ShouldBe(2);
        permissions.ShouldContain(p => p.Permission == Permission.Read && p.Source == $"Role:{readersRole}");
        permissions.ShouldContain(p => p.Permission == Permission.Write && p.Source == $"Role:{writersRole}");
    }

    [Fact]
    public async Task GetPermissions_TypeLevel_ShouldReturnOnlyTypeWidePermissions()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Admins_{DateTime.UtcNow.Ticks}";

        // Grant mix of type-wide and entity-specific permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), null, Permission.Export);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);

        // Act
        var typePermissions = await this.evaluator.GetPermissionsAsync(userId, [roleName]);

        // Assert
        typePermissions.Count.ShouldBe(2, "Should only return type-wide permissions");
        typePermissions.ShouldContain(p => p.Permission == Permission.List && p.Source == "Direct");
        typePermissions.ShouldContain(p => p.Permission == Permission.Export && p.Source == $"Role:{roleName}");
    }

    [Fact]
    public async Task HasPermission_WithDefaultProvider_ShouldCombineAllSources()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Users_{DateTime.UtcNow.Ticks}";

        // Setup services with default provider
        var services = new ServiceCollection();
        services.AddSingleton(this.dbContext);
        services.AddSingleton(this.loggerFactory);

        services.AddEntityAuthorization(o =>
           o.WithEntityPermissions<StubDbContext>(e =>
           {
               e.AddEntity<PersonStub>();
               e.UseDefaultPermissionProvider<PersonStub, TestDefaultPermissionProvider>([Permission.Read]);
           })
        );

        var localServiceProvider = services.BuildServiceProvider();
        var localEvaluator = localServiceProvider.GetRequiredService<IEntityPermissionEvaluator<PersonStub>>();

        // Grant some regular permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Write);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Update);

        // Act
        var permissions = await localEvaluator.GetPermissionsAsync(userId, [roleName], person);

        // Assert
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(p => p.Permission == Permission.Write && p.Source == "Direct");
        permissions.ShouldContain(p => p.Permission == Permission.Update && p.Source == $"Role:{roleName}");
        permissions.ShouldContain(p => p.Permission == Permission.Read && p.Source.StartsWith("Default:"));
    }

    [Fact]
    public async Task GetPermissions_WithNoPermissionsGranted_ShouldReturnEmptyCollection()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Act
        var entityPermissions = await this.evaluator.GetPermissionsAsync(userId, [], person);
        var typePermissions = await this.evaluator.GetPermissionsAsync(userId, []);

        // Assert
        entityPermissions.ShouldNotBeNull();
        entityPermissions.Count.ShouldBe(0);
        typePermissions.ShouldNotBeNull();
        typePermissions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task HasPermission_WithMultiplePermissionsOnSameEntity_ShouldWorkCorrectly()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant multiple permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Write);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.Delete);

        // Act & Assert
        var hasRead = await this.evaluator.HasPermissionAsync(userId, [], person, Permission.Read);
        var hasWrite = await this.evaluator.HasPermissionAsync(userId, [], person, Permission.Write);
        var hasDelete = await this.evaluator.HasPermissionAsync(userId, [], person, Permission.Delete);
        var hasExport = await this.evaluator.HasPermissionAsync(userId, [], person, Permission.Export);

        hasRead.ShouldBeTrue();
        hasWrite.ShouldBeTrue();
        hasDelete.ShouldBeTrue();
        hasExport.ShouldBeFalse();

        var permissions = await this.evaluator.GetPermissionsAsync(userId, [], person);
        permissions.Count.ShouldBe(3);
        permissions.Select(p => p.Permission).ShouldContain(Permission.Read.ToString());
        permissions.Select(p => p.Permission).ShouldContain(Permission.Write.ToString());
        permissions.Select(p => p.Permission).ShouldContain(Permission.Delete.ToString());
    }

    [Fact]
    public async Task HasPermission_RoleAndDirectPermissions_ShouldNotInterfere()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Role_{DateTime.UtcNow.Ticks}";

        // Grant different permissions to user and role
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);

        // Act & Assert
        // With no role
        var permissions1 = await this.evaluator.GetPermissionsAsync(userId, [], person);
        permissions1.Count.ShouldBe(1);
        permissions1.ShouldContain(p => p.Permission == Permission.Read && p.Source == "Direct");

        // With role
        var permissions2 = await this.evaluator.GetPermissionsAsync(userId, [roleName], person);
        permissions2.Count.ShouldBe(2);
        permissions2.ShouldContain(p => p.Permission == Permission.Read && p.Source == "Direct");
        permissions2.ShouldContain(p => p.Permission == Permission.Write && p.Source == $"Role:{roleName}");
    }

    [Fact]
    public async Task GetPermissions_WithMixOfTypeWideAndEntitySpecific_ShouldIncludeAll()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Role_{DateTime.UtcNow.Ticks}";

        // Grant mix of permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), null, Permission.Export);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), person.Id, Permission.Write);

        // Act
        var entityPermissions = await this.evaluator.GetPermissionsAsync(userId, [roleName], person);

        // Assert
        entityPermissions.Count.ShouldBe(4);
        entityPermissions.ShouldContain(p => p.Permission == Permission.List && p.Source == "Direct");
        entityPermissions.ShouldContain(p => p.Permission == Permission.Read && p.Source == "Direct");
        entityPermissions.ShouldContain(p => p.Permission == Permission.Export && p.Source == $"Role:{roleName}");
        entityPermissions.ShouldContain(p => p.Permission == Permission.Write && p.Source == $"Role:{roleName}");
    }

    [Fact]
    public async Task HasPermission_WithRevokedPermissions_ShouldWorkCorrectly()
    {
        // Arrange
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John" };
        await this.dbContext.Set<PersonStub>().AddAsync(person);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roelName = $"Role_{DateTime.UtcNow.Ticks}";

        // Grant and then revoke permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(roelName, nameof(PersonStub), person.Id, Permission.Write);

        var hasReadBefore = await this.evaluator.HasPermissionAsync(userId, [roelName], person, Permission.Read);
        hasReadBefore.ShouldBeTrue();

        // Revoke permissions
        await this.provider.RevokeUserPermissionAsync(userId, nameof(PersonStub), person.Id, Permission.Read);
        await this.provider.RevokeRolePermissionAsync(roelName, nameof(PersonStub), person.Id, Permission.Write);

        // Act & Assert
        var hasReadAfter = await this.evaluator.HasPermissionAsync(userId, [roelName], person, Permission.Read);
        var hasWriteAfter = await this.evaluator.HasPermissionAsync(userId, [roelName], person, Permission.Write);

        hasReadAfter.ShouldBeFalse();
        hasWriteAfter.ShouldBeFalse();

        var permissions = await this.evaluator.GetPermissionsAsync(userId, [roelName], person);
        permissions.Count.ShouldBe(0);
    }

    private class TestDefaultPermissionProvider : IDefaultEntityPermissionProvider<PersonStub>
    {
        public HashSet<string> GetDefaultPermissions() => [Permission.Read];
    }
}
