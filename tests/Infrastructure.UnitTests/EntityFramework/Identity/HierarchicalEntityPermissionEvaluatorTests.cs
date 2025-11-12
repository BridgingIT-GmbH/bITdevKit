// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Identity;

using System;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class HierarchicalEntityPermissionEvaluatorTests : IClassFixture<StubDbContextFixture>
{
    private readonly ILoggerFactory loggerFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IEntityPermissionProvider provider;
    private readonly IEntityPermissionEvaluator<PersonStub> evaluator;
    private readonly StubDbContext dbContext;

    public HierarchicalEntityPermissionEvaluatorTests(StubDbContextFixture fixture)
    {
        this.loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        var services = new ServiceCollection();
        services.AddSingleton(fixture.Context);
        services.AddSingleton(this.loggerFactory);

        services.AddEntityAuthorization(o =>
            o.WithEntityPermissions<StubDbContext>(e =>
            {
                //e.AddEntity<PersonStub>() for non hierarchical entities
                e.AddHierarchicalEntity<PersonStub>(p => p.ManagerId);
            })
        );

        this.serviceProvider = services.BuildServiceProvider();
        this.dbContext = fixture.Context;
        this.provider = this.serviceProvider.GetRequiredService<IEntityPermissionProvider>();
        this.evaluator = this.serviceProvider.GetRequiredService<IEntityPermissionEvaluator<PersonStub>>();
    }

    [Fact]
    public async Task HasPermission_DirectManagerPermission_ShouldGrantAccess()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id }; // has READ permission
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant permission to manager
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Read); // set READ permission for manager

        // Act & Assert
        var hasManagerAccess = await this.evaluator.HasPermissionAsync(userId, [], manager, Permission.Read); // has permission (direct)
        hasManagerAccess.ShouldBeTrue("Should have direct access to manager");

        var hasEmployeeAccess = await this.evaluator.HasPermissionAsync(userId, [], employee, Permission.Read); // permission from manager (inherit)
        hasEmployeeAccess.ShouldBeTrue("Should have inherited permission from parent");

        var hasCeoAccess = await this.evaluator.HasPermissionAsync(userId, [], ceo, Permission.Read); // has no permission (no grant or inherit)
        hasCeoAccess.ShouldBeFalse("Should not have access to higher level");
    }

    [Fact]
    public async Task HasPermission_InheritedFromManager_ShouldGrantAccess()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" }; // has READ permission
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant permission to CEO (top of chain)
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), ceo.Id, Permission.Read);

        // Act & Assert
        var hasManagerAccess = await this.evaluator.HasPermissionAsync(userId, [], manager, Permission.Read);
        hasManagerAccess.ShouldBeTrue("Should inherit from CEO");

        var hasEmployeeAccess = await this.evaluator.HasPermissionAsync(userId, [], employee, Permission.Read);
        hasEmployeeAccess.ShouldBeTrue("Should inherit from CEO through manager");

        // Grant additional permission to manager
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);

        // Verify inherited permissions
        var employeePermissions = await this.evaluator.GetPermissionsAsync(userId, [], employee);
        employeePermissions.Count.ShouldBe(2);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Read);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Write);
    }

    [Fact]
    public async Task HasPermission_NoGrants_ShouldNotGrantAccess()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" }; // has READ permission
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Act & Assert
        var hasManagerAccess = await this.evaluator.HasPermissionAsync(userId, [], manager, Permission.Read);
        hasManagerAccess.ShouldBeFalse();

        var hasEmployeeAccess = await this.evaluator.HasPermissionAsync(userId, [], employee, Permission.Read);
        hasEmployeeAccess.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermissionId_NoGrants_ShouldNotGrantAccess()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" }; // has READ permission
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Act & Assert
        var hasManagerAccess = await this.evaluator.HasPermissionAsync(userId, [], manager.Id, Permission.Read);
        hasManagerAccess.ShouldBeFalse();

        var hasEmployeeAccess = await this.evaluator.HasPermissionAsync(userId, [], employee.Id, Permission.Read);
        hasEmployeeAccess.ShouldBeFalse();
    }

    //[Fact]
    //public async Task GetPermissions_ShouldShowInheritanceChain()
    //{
    //    // Arrange
    //    var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
    //    var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
    //    var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

    //    await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
    //    await this.dbContext.SaveChangesAsync();

    //    var userId = DateTime.UtcNow.Ticks.ToString();
    //    var roleName = $"Role_{DateTime.UtcNow.Ticks}";

    //    // Setup permissions at different levels
    //    await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), employee.Id, Permission.Read);
    //    await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
    //    await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), ceo.Id, Permission.Delete);

    //    // Act
    //    var permissions = await this.evaluator.GetPermissionsAsync(userId, [roleName], employee);

    //    // Assert
    //    permissions.Count.ShouldBe(3);
    //    permissions.ShouldContain(p => p.Permission == Permission.Read && p.Source == "Direct");
    //    permissions.ShouldContain(p => p.Permission == Permission.Write && p.Source == "Parent");
    //    permissions.ShouldContain(p => p.Permission == Permission.Delete && p.Source.StartsWith("Parent:Role"));
    //}

    [Fact]
    public async Task HasPermission_WithMultipleEmployees_ShouldRespectHierarchy()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id }; // has READ permission
        var employee1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee1", ManagerId = manager.Id };
        var employee2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee2", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee1, employee2]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant permission to manager
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Read);

        // Act & Assert
        var employee1Permissions = await this.evaluator.GetPermissionsAsync(userId, [], employee1);
        var employee2Permissions = await this.evaluator.GetPermissionsAsync(userId, [], employee2);

        // Both employees should inherit the same permissions
        employee1Permissions.Count.ShouldBe(1);
        employee2Permissions.Count.ShouldBe(1);
        employee1Permissions.First().Permission.ShouldBe(Permission.Read);
        employee2Permissions.First().Permission.ShouldBe(Permission.Read);
    }

    [Fact]
    public async Task HasPermission_TypeWideAndHierarchy_ShouldCombineCorrectly()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" }; // LIST
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id }; // LIST + WRITE
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id }; // LIST + WRITE + DELETE

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant type-wide list permission
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);

        // Grant specific permission to manager
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);

        // Grant specific permission to employee
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), employee.Id, Permission.Delete);

        // Act
        var typePermissions = await this.evaluator.GetPermissionsAsync(userId, []);
        var employeePermissions = await this.evaluator.GetPermissionsAsync(userId, [], employee);

        // Assert
        // Type-wide permissions
        typePermissions.Count.ShouldBe(1);
        typePermissions.ShouldContain(p => p.Permission == Permission.List);

        // Employee should have both inherited Write, direct Delete and type-wide List
        //employeePermissions.Count.ShouldBe(5);
        employeePermissions.ShouldContain(p => p.Permission == Permission.List);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Write);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Delete);

        // Verify actual permission checks
        var hasListPermission = await this.evaluator.HasPermissionAsync(userId, [], Permission.List);
        hasListPermission.ShouldBeTrue("Should have type-wide list permission");

        var hasWritePermission = await this.evaluator.HasPermissionAsync(userId, [], employee, Permission.Write);
        hasWritePermission.ShouldBeTrue("Should have inherited write permission");
    }

    [Fact]
    public async Task RevokePermission_ShouldAffectInheritanceChain()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Role_{DateTime.UtcNow.Ticks}";

        // Setup initial permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), ceo.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), ceo.Id, Permission.Export);

        // Verify initial state
        var initialPermissions = await this.evaluator.GetPermissionsAsync(userId, [roleName], employee);
        initialPermissions.Count.ShouldBe(3);

        // Act - Revoke permissions at different levels
        await this.provider.RevokeUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
        await this.provider.RevokeRolePermissionAsync(roleName, nameof(PersonStub), ceo.Id, Permission.Export);

        // Assert
        var finalPermissions = await this.evaluator.GetPermissionsAsync(userId, [roleName], employee);
        finalPermissions.Count.ShouldBe(1, "Should only have permissions from CEO");
        finalPermissions.ShouldContain(p => p.Permission == Permission.Read);

        // Verify specific permissions
        var hasWritePermission = await this.evaluator.HasPermissionAsync(
            userId,
            [roleName],
            employee,
            Permission.Write);
        hasWritePermission.ShouldBeFalse("Write permission should be revoked");

        var hasExportPermission = await this.evaluator.HasPermissionAsync(
            userId,
            [roleName],
            employee,
            Permission.Export);
        hasExportPermission.ShouldBeFalse("Export permission should be revoked");

        var hasReadPermission = await this.evaluator.HasPermissionAsync(
            userId,
            [roleName],
            employee,
            Permission.Read);
        hasReadPermission.ShouldBeTrue("Should still have inherited Read permission from CEO");
    }

    [Fact]
    public async Task RevokePermission_WithTypeWideAndHierarchy_ShouldWorkCorrectly()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" }; // LIST + READ
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id }; // LIST + READ + WRITE
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id }; // LIST + READ + WRITE

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Setup type-wide and hierarchical permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), ceo.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), employee.Id, Permission.Delete);

        // Verify initial state
        var initialTypePermissions = await this.evaluator.GetPermissionsAsync(userId, []);
        var initialEmployeePermissions = await this.evaluator.GetPermissionsAsync(userId, [], employee);

        initialTypePermissions.Count.ShouldBe(1);
        //initialEmployeePermissions.Count.ShouldBe(6);

        // Act - Revoke permissions
        await this.provider.RevokeUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);
        await this.provider.RevokeUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
        await this.provider.RevokeUserPermissionAsync(userId, nameof(PersonStub), employee.Id, Permission.Delete);

        // Assert
        var finalTypePermissions = await this.evaluator.GetPermissionsAsync(userId, []);
        finalTypePermissions.Count.ShouldBe(0, "Should have no type-wide permissions");

        var finalEmployeePermissions = await this.evaluator.GetPermissionsAsync(userId, [], employee);
        finalEmployeePermissions.Count.ShouldBe(1, "Should only have inherited Read permission");
        finalEmployeePermissions.ShouldContain(p => p.Permission == Permission.Read);

        // Verify specific permissions are properly revoked
        var hasListPermission = await this.evaluator.HasPermissionAsync(userId, [], Permission.List);
        hasListPermission.ShouldBeFalse("Type-wide List permission should be revoked");

        var hasWritePermission = await this.evaluator.HasPermissionAsync(userId, [], employee, Permission.Write);
        hasWritePermission.ShouldBeFalse("Write permission should be revoked");

        var hasReadPermission = await this.evaluator.HasPermissionAsync(userId, [], employee, Permission.Read);
        hasReadPermission.ShouldBeTrue("Should still have inherited Read permission from CEO");
    }

    [Fact]
    public async Task GetPermissions_ShouldShowInheritanceChain()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var roleName = $"Role_{DateTime.UtcNow.Ticks}";

        // Setup permissions at different levels
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), ceo.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
        await this.provider.GrantRolePermissionAsync(roleName, nameof(PersonStub), manager.Id, Permission.Delete);

        // Act - get employee permissions
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [roleName], employee);

        // Assert
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(p => p.Permission == Permission.Read);
        permissions.ShouldContain(p => p.Permission == Permission.Write);
        permissions.ShouldContain(p => p.Permission == Permission.Delete && p.Source.StartsWith("Parent:Role"));
    }

    [Fact]
    public async Task HasPermission_WithMultipleEmployees_ShouldInheritSamePermissions()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee1", ManagerId = manager.Id };
        var employee2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee2", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee1, employee2]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant permissions to manager
        await this.provider.GrantUserPermissionAsync(
            userId,
            nameof(PersonStub),
            manager.Id,
            Permission.Read);

        // Act & Assert
        var employee1Permissions = await this.evaluator.GetPermissionsAsync(userId, [], employee1);
        var employee2Permissions = await this.evaluator.GetPermissionsAsync(userId, [], employee2);

        // Both employees should inherit the same permissions
        employee1Permissions.Count.ShouldBe(1);
        employee2Permissions.Count.ShouldBe(1);
        employee1Permissions.First().Permission.ShouldBe(Permission.Read);
        employee2Permissions.First().Permission.ShouldBe(Permission.Read);
    }

    [Fact]
    public async Task HasPermission_DeepHierarchy_ShouldInheritCorrectly()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var director = new PersonStub { Id = Guid.NewGuid(), FirstName = "Director", ManagerId = ceo.Id };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = director.Id };
        var teamLead = new PersonStub { Id = Guid.NewGuid(), FirstName = "TeamLead", ManagerId = manager.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = teamLead.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, director, manager, teamLead, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant permissions at different levels
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), ceo.Id, Permission.List);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), director.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Write);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), teamLead.Id, Permission.Delete);

        // Act & Assert
        var employeePermissions = await this.evaluator.GetPermissionsAsync(userId, [], employee); // lowest level in hierarchy
        employeePermissions.Count.ShouldBe(4);

        // Verify each level of inheritance
        employeePermissions.ShouldContain(p => p.Permission == Permission.List);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Read);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Write);
        employeePermissions.ShouldContain(p => p.Permission == Permission.Delete);
    }

    [Fact]
    public async Task HasPermission_CircularHierarchy_ShouldStopAtOriginalEntity()
    {
        // Arrange
        var person1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person1" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person2" };

        // Create circular reference
        person1.ManagerId = person2.Id;
        person2.ManagerId = person1.Id;

        await this.dbContext.Set<PersonStub>().AddRangeAsync([person1, person2]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person1.Id, Permission.Read);

        // Act
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [], person2);

        // Assert
        permissions.Count.ShouldBe(1);
        permissions.ShouldContain(p => p.Permission == Permission.Read);
    }

    [Fact]
    public async Task HasPermission_CrossCuttingRoles_ShouldCombineCorrectly()
    {
        // Arrange
        var ceo = new PersonStub { Id = Guid.NewGuid(), FirstName = "CEO" };
        var manager = new PersonStub { Id = Guid.NewGuid(), FirstName = "Manager", ManagerId = ceo.Id };
        var employee = new PersonStub { Id = Guid.NewGuid(), FirstName = "Employee", ManagerId = manager.Id };

        await this.dbContext.Set<PersonStub>().AddRangeAsync([ceo, manager, employee]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        var hrRole = $"HR_{DateTime.UtcNow.Ticks}";
        var adminRole = $"Admin_{DateTime.UtcNow.Ticks}";

        // Grant cross-cutting permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), manager.Id, Permission.Read);
        await this.provider.GrantRolePermissionAsync(hrRole, nameof(PersonStub), ceo.Id, Permission.Write);
        await this.provider.GrantRolePermissionAsync(adminRole, nameof(PersonStub), employee.Id, Permission.Delete);

        // Act
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [hrRole, adminRole], employee);

        // Assert
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(p => p.Permission == Permission.Read);
        permissions.ShouldContain(p => p.Permission == Permission.Write);
        permissions.ShouldContain(p => p.Permission == Permission.Delete);
    }

    [Fact]
    public async Task HasPermission_ThreeNodeCircle_ShouldStopAtOriginalEntity()
    {
        // Arrange
        var person1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person1" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person2" };
        var person3 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person3" };

        // Create circular reference: person1 -> person2 -> person3 -> person1
        person1.ManagerId = person2.Id;
        person2.ManagerId = person3.Id;
        person3.ManagerId = person1.Id;

        await this.dbContext.Set<PersonStub>().AddRangeAsync([person1, person2, person3]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person1.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person2.Id, Permission.Write);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person3.Id, Permission.Delete);

        // Act & Assert
        var permissions1 = await this.evaluator.GetPermissionsAsync(userId, [], person1);
        permissions1.Count.ShouldBe(4);

        var permissions2 = await this.evaluator.GetPermissionsAsync(userId, [], person2);
        permissions2.Count.ShouldBe(4);

        var permissions3 = await this.evaluator.GetPermissionsAsync(userId, [], person3);
        permissions3.Count.ShouldBe(4);
    }

    [Fact]
    public async Task HasPermission_CircleWithBranch_ShouldStopAtCircleButIncludeBranch()
    {
        // Arrange
        var person1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person1" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person2" };
        var person3 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person3" }; // Part of circle
        var person4 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person4" }; // Branch

        // Create circular reference with a branch
        person1.ManagerId = person2.Id;
        person2.ManagerId = person3.Id;
        person3.ManagerId = person1.Id;
        person4.ManagerId = person2.Id; // Branch off person2

        await this.dbContext.Set<PersonStub>().AddRangeAsync([person1, person2, person3, person4]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person1.Id, Permission.Read);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person2.Id, Permission.Write);

        // Act
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [], person4);

        // Assert
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(p => p.Permission == Permission.Read);
        permissions.ShouldContain(p => p.Permission == Permission.Write);
    }

    [Fact]
    public async Task HasPermission_CircularWithTypeWidePermission_ShouldWorkCorrectly()
    {
        // Arrange
        var person1 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person1" };
        var person2 = new PersonStub { Id = Guid.NewGuid(), FirstName = "Person2" };

        // Create circular reference
        person1.ManagerId = person2.Id;
        person2.ManagerId = person1.Id;

        await this.dbContext.Set<PersonStub>().AddRangeAsync([person1, person2]);
        await this.dbContext.SaveChangesAsync();

        var userId = DateTime.UtcNow.Ticks.ToString();

        // Grant both specific and type-wide permissions
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), null, Permission.List);
        await this.provider.GrantUserPermissionAsync(userId, nameof(PersonStub), person1.Id, Permission.Read);

        // Act
        var permissions = await this.evaluator.GetPermissionsAsync(userId, [], person2);

        // Assert
        permissions.Count.ShouldBe(4);
        permissions.ShouldContain(p => p.Permission == Permission.List);
        permissions.ShouldContain(p => p.Permission == Permission.Read);
    }
}