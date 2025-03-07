// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Identity;

using Xunit;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Identity;
using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class EntityPermissionEvaluatorTests
{
    private readonly ILoggerFactory loggerFactory;
    private readonly IEntityPermissionProvider provider;
    private readonly IDefaultEntityPermissionProvider<TestEntity> defaultProvider;
    private readonly ICacheProvider cacheProvider;
    private readonly ICurrentUserAccessor userAccessor;
    private readonly EntityPermissionOptions options;

    public EntityPermissionEvaluatorTests()
    {
        this.loggerFactory = Substitute.For<ILoggerFactory>();
        this.loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger<EntityPermissionEvaluator<TestEntity>>>());
        this.provider = Substitute.For<IEntityPermissionProvider>();
        this.defaultProvider = Substitute.For<IDefaultEntityPermissionProvider<TestEntity>>();
        this.cacheProvider = Substitute.For<ICacheProvider>();
        this.userAccessor = Substitute.For<ICurrentUserAccessor>();
        this.options = new EntityPermissionOptions { EnableCaching = true, CacheLifetime = TimeSpan.FromMinutes(5) };
        this.options.AddEntity<TestEntity>();

        this.userAccessor.UserId.Returns("user123");
        this.userAccessor.Roles.Returns(["role1", "role2"]);
    }

    [Fact]
    public async Task HasPermissionAsync_EntityWide_CacheHit_ReturnsTrue()
    {
        // Arrange
        var cacheEntry = new EntityPermissionCacheEntry();
        cacheEntry.AddPermission("Read", "Direct");
        this.cacheProvider.TryGet(Arg.Any<string>(), out Arg.Any<EntityPermissionCacheEntry>())
            .Returns(x => { x[1] = cacheEntry; return true; });
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, [this.defaultProvider], this.cacheProvider, this.options);

        // Act
        var result = await evaluator.HasPermissionAsync(this.userAccessor, "Read");

        // Assert
        result.ShouldBeTrue();
        await this.provider.DidNotReceive().HasPermissionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HasPermissionAsync_EntityWide_DirectPermission_ReturnsTrue()
    {
        // Arrange
        this.provider.HasPermissionAsync("user123", Arg.Any<string[]>(), typeof(TestEntity).FullName, "Read", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, [this.defaultProvider], this.cacheProvider, this.options);

        // Act
        var result = await evaluator.HasPermissionAsync(this.userAccessor, "Read");

        // Assert
        result.ShouldBeTrue();
        this.cacheProvider.Received().Set(Arg.Any<string>(), Arg.Any<EntityPermissionCacheEntry>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task HasPermissionAsync_EntitySpecific_DefaultPermission_ReturnsTrue()
    {
        // Arrange
        var entity = new TestEntity();
        this.defaultProvider.GetDefaultPermissions().Returns(["Write"]);
        this.provider.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, [this.defaultProvider], this.cacheProvider, this.options);

        // Act
        var result = await evaluator.HasPermissionAsync(this.userAccessor, entity, "Write");

        // Assert
        result.ShouldBeTrue();
        this.cacheProvider.Received().Set(Arg.Any<string>(), Arg.Any<EntityPermissionCacheEntry>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task HasPermissionAsync_NoPermission_ReturnsFalse()
    {
        // Arrange
        this.provider.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, [this.defaultProvider], null, new EntityPermissionOptions());

        // Act
        var result = await evaluator.HasPermissionAsync(this.userAccessor, "Read");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPermissionsAsync_EntityWide_DirectAndRolePermissions_ReturnsCombined()
    {
        // Arrange
        this.provider.GetUserPermissionsAsync("user123", typeof(TestEntity).FullName, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new[] { "Read" } as IReadOnlyCollection<string>));
        this.provider.GetRolePermissionsAsync("role1", typeof(TestEntity).FullName, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new[] { "Write" } as IReadOnlyCollection<string>));
        this.provider.GetRolePermissionsAsync("role2", typeof(TestEntity).FullName, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new[] { "Delete" } as IReadOnlyCollection<string>));
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, [this.defaultProvider], this.cacheProvider, this.options);

        // Act
        var permissions = await evaluator.GetPermissionsAsync(this.userAccessor);

        // Assert
        permissions.Count.ShouldBe(3);
        permissions.ShouldContain(p => p.Permission == "Read" && p.Source == "Direct");
        permissions.ShouldContain(p => p.Permission == "Write" && p.Source == "Role:role1");
        permissions.ShouldContain(p => p.Permission == "Delete" && p.Source == "Role:role2");
    }

    [Fact]
    public async Task GetPermissionsAsync_IdSpecific_NoPermissions_ReturnsEmpty()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        this.provider.GetUserPermissionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<string>() as IReadOnlyCollection<string>));
        this.provider.GetRolePermissionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<string>() as IReadOnlyCollection<string>));
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, Enumerable.Empty<IDefaultEntityPermissionProvider<TestEntity>>(), null, new EntityPermissionOptions().AddEntity<TestEntity>());

        // Act
        var permissions = await evaluator.GetPermissionsAsync(this.userAccessor, entityId);

        // Assert
        permissions.ShouldBeEmpty();
    }

    [Fact]
    public void HasPermissionAsync_NullUserAccessor_ThrowsArgumentNullException()
    {
        // Arrange
        var evaluator = new EntityPermissionEvaluator<TestEntity>(
            this.loggerFactory, this.provider, [this.defaultProvider], this.cacheProvider, this.options);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => evaluator.HasPermissionAsync(null, "Read"))
            .ParamName.ShouldBe("currentUserAccessor");
    }
}
