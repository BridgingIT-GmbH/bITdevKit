// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Identity;

using Xunit;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using NSubstitute;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Identity;

// Test entity for IEntity implementation
public class TestEntity : Entity<Guid>
{
    public TestEntity()
    {
        this.Id = Guid.NewGuid();
    }
}

public class HasPermissionRuleTests
{
    private readonly ICurrentUserAccessor userAccessor;
    private readonly IEntityPermissionEvaluator<TestEntity> permissionEvaluator;

    public HasPermissionRuleTests()
    {
        this.userAccessor = Substitute.For<ICurrentUserAccessor>();
        this.permissionEvaluator = Substitute.For<IEntityPermissionEvaluator<TestEntity>>();
    }

    [Fact]
    public async Task EntityWide_HasPermission_ReturnsSuccess()
    {
        // Arrange
        this.permissionEvaluator.HasPermissionAsync(this.userAccessor, typeof(TestEntity), "Read", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, "Read");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EntityWide_NoPermission_ReturnsFailure()
    {
        // Arrange
        this.permissionEvaluator.HasPermissionAsync(this.userAccessor, "Read", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, "Read");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == "Unauthorized: User must have Read permission for entity TestEntity");
    }

    [Fact]
    public async Task EntitySpecific_HasPermission_ReturnsSuccess()
    {
        // Arrange
        var entity = new TestEntity();
        this.permissionEvaluator.HasPermissionAsync(this.userAccessor, entity, "Write", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, entity, "Write");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EntitySpecific_NoPermission_ReturnsFailureWithMessage()
    {
        // Arrange
        var entity = new TestEntity();
        this.permissionEvaluator.HasPermissionAsync(this.userAccessor, "Write", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, entity, "Write");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == $"Unauthorized: User must have Write permission for entity TestEntity with id {entity.Id}");
    }

    [Fact]
    public async Task IdSpecific_HasPermission_ReturnsSuccess()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.permissionEvaluator.HasPermissionAsync(this.userAccessor, entityId, "Delete", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, entityId, "Delete");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task IdSpecific_NoPermission_ReturnsFailureWithMessage()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.permissionEvaluator.HasPermissionAsync(this.userAccessor, entityId, "Delete", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, entityId, "Delete");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == $"Unauthorized: User must have Delete permission for entity TestEntity with id {entityId}");
    }

    [Fact]
    public void Constructor_NullPermission_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, null))
            .Message.ShouldContain("permission");
    }

    [Fact]
    public void Constructor_NullEntity_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, null, "Read"))
            .Message.ShouldContain("entity");
    }

    [Fact]
    public void Constructor_NullEntityId_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasPermissionRule<TestEntity>(this.userAccessor, this.permissionEvaluator, (object)null, "Read"))
            .Message.ShouldContain("entityId");
    }
}
