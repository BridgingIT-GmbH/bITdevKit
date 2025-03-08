// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Identity;

using Xunit;
using BridgingIT.DevKit.Common;
using NSubstitute;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Identity;

public class HasNotPermissionRuleTests
{
    private readonly ICurrentUserAccessor userAccessor;
    private readonly IEntityPermissionEvaluator<TestEntity> evaluator;

    public HasNotPermissionRuleTests()
    {
        this.userAccessor = Substitute.For<ICurrentUserAccessor>();
        this.evaluator = Substitute.For<IEntityPermissionEvaluator<TestEntity>>();
    }

    [Fact]
    public async Task EntityWide_NoPermission_ReturnsSuccess()
    {
        // Arrange
        this.evaluator.HasPermissionAsync(this.userAccessor, "Read", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, "Read");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EntityWide_HasPermission_ReturnsFailure()
    {
        // Arrange
        this.evaluator.HasPermissionAsync(this.userAccessor, "Read", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, "Read");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == "Unauthorized: User must not have Read permission for entity TestEntity");
    }

    [Fact]
    public async Task EntitySpecific_NoPermission_ReturnsSuccess()
    {
        // Arrange
        var entity = new TestEntity();
        this.evaluator.HasPermissionAsync(this.userAccessor, entity, "Write", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entity, "Write");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EntitySpecific_HasPermission_ReturnsFailureWithMessage()
    {
        // Arrange
        var entity = new TestEntity();
        this.evaluator.HasPermissionAsync(this.userAccessor, entity, "Write", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entity, "Write");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == $"Unauthorized: User must not have Write permission for entity TestEntity with id {entity.Id}");
    }

    [Fact]
    public async Task IdSpecific_NoPermission_ReturnsSuccess()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.evaluator.HasPermissionAsync(this.userAccessor, entityId, "Delete", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entityId, "Delete");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task IdSpecific_HasPermission_ReturnsFailureWithMessage()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.evaluator.HasPermissionAsync(this.userAccessor, entityId, "Delete", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entityId, "Delete");

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == $"Unauthorized: User must not have Delete permission for entity TestEntity with id {entityId}");
    }

    [Fact]
    public void Constructor_NullEntity_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, null, "Read"))
            .Message.ShouldContain("entity");
    }

    [Fact]
    public void Constructor_NullEntityId_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, (object)null, "Read"))
            .Message.ShouldContain("entityId");
    }

    [Fact]
    public async Task EntityWide_NoPermissions_ReturnsSuccess()
    {
        // Arrange
        this.evaluator.HasPermissionAsync(this.userAccessor, Arg.Any<object>(), Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, ["Read", "Write"]);

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EntityWide_HasAnyPermission_ReturnsFailure()
    {
        // Arrange
        this.evaluator.HasPermissionAsync(this.userAccessor, Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, ["Read", "Write"]);

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == "Unauthorized: User must not have any of [Read, Write] permissions for entity TestEntity");
    }

    [Fact]
    public async Task EntitySpecific_NoPermissions_ReturnsSuccess()
    {
        // Arrange
        var entity = new TestEntity();
        this.evaluator.HasPermissionAsync(this.userAccessor, Arg.Any<TestEntity>(), Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entity, ["Read", "Write"]);

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EntitySpecific_HasAnyPermission_ReturnsFailureWithMessage()
    {
        // Arrange
        var entity = new TestEntity();
        this.evaluator.HasPermissionAsync(this.userAccessor, Arg.Any<TestEntity>(), Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entity, ["Read", "Write"]);

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == $"Unauthorized: User must not have any of [Read, Write] permissions for entity TestEntity with id {entity.Id}");
    }

    [Fact]
    public async Task IdSpecific_NoPermissions_ReturnsSuccess()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.evaluator.HasPermissionAsync(this.userAccessor, Arg.Any<object>(), Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entityId, ["Read", "Delete"]);

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task IdSpecific_HasAnyPermission_ReturnsFailureWithMessage()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.evaluator.HasPermissionAsync(this.userAccessor, Arg.Any<object>(), Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var rule = new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, entityId, ["Read", "Delete"]);

        // Act
        var result = await rule.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message == $"Unauthorized: User must not have any of [Read, Delete] permissions for entity TestEntity with id {entityId}");
    }

    [Fact]
    public void Constructor_NullPermissionsArray_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, (string[])null))
            .Message.ShouldContain("permissions");
    }

    [Fact]
    public void Constructor_EmptyPermissionsArray_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new HasNotPermissionRule<TestEntity>(this.userAccessor, this.evaluator, []));
    }
}