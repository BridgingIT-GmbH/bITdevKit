// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using System.Security.Claims;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public class EntitiesDashboardOptionsTests
{
    [Fact]
    public void Defaults_HaveDocumentedLimitsAndNoEditConfiguration()
    {
        // Arrange & Act
        var sut = new EntitiesDashboardOptions();

        // Assert
        sut.BatchSize.ShouldBe(50);
        sut.MaxBatchSize.ShouldBe(200);
        sut.MaxExportRows.ShouldBe(5000);
        sut.MaxBulkSelection.ShouldBe(100);
        sut.IsEditConfigured.ShouldBeFalse();
    }

    [Theory]
    [InlineData(50, 200, 50)]
    [InlineData(500, 200, 200)]
    [InlineData(0, 200, 1)]
    [InlineData(-3, 200, 1)]
    public void EffectiveBatchSize_IsClampedToTheMaximum(int batch, int max, int expected)
    {
        // Arrange
        var sut = new EntitiesDashboardOptions { BatchSize = batch, MaxBatchSize = max };

        // Act & Assert
        sut.EffectiveBatchSize.ShouldBe(expected);
    }

    [Fact]
    public void PageSizes_DefaultOptions_ContainsPresetsUpToMaxAndTheBatchSize()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();

        // Act & Assert
        sut.PageSizes.ShouldBe([10, 25, 50, 100, 200]);
    }

    [Fact]
    public void PageSizes_CustomBatchSize_IsIncludedInOrder()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions { BatchSize = 30, MaxBatchSize = 60 };

        // Act & Assert
        sut.PageSizes.ShouldBe([10, 25, 30, 50]);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(10, 10)]
    [InlineData(1000, 200)]
    public void ResolveBatchSize_ClampsRequestedSize(int requested, int expected)
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();

        // Act & Assert
        sut.ResolveBatchSize(requested).ShouldBe(expected);
    }

    [Fact]
    public void IsEditConfigured_PolicyOrRoles_IsTrue()
    {
        // Arrange & Act & Assert
        new EntitiesDashboardOptions { EditPolicy = "Editors" }.IsEditConfigured.ShouldBeTrue();
        new EntitiesDashboardOptions { EditRoles = ["admin"] }.IsEditConfigured.ShouldBeTrue();
        new EntitiesDashboardOptions { EditPolicy = " " }.IsEditConfigured.ShouldBeFalse();
    }

    [Fact]
    public void ExcludeAndInclude_ToggleEachOther()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();

        // Act
        sut.Exclude<Customer>();
        sut.Include<Customer>();
        sut.Exclude<Region>();

        // Assert
        sut.IncludedTypes.ShouldContain(typeof(Customer));
        sut.ExcludedTypes.ShouldNotContain(typeof(Customer));
        sut.ExcludedTypes.ShouldContain(typeof(Region));
    }

    [Fact]
    public void EntityBuilder_MaskHideAndExclude_RecordRules()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();

        // Act
        sut.Entity<Customer>(e => e
            .Mask(c => c.ApiToken)
            .Mask(c => c.Address.TaxNumber, "##")
            .Hide(c => c.InternalNote)
            .Hide("Address.Street")
            .Mask(c => c.Age)
            .AllowNonPublicSetters());

        // Assert
        var configuration = sut.EntityConfigurations[typeof(Customer)];
        configuration.AllowNonPublicSetters.ShouldBeTrue();
        configuration.Properties["ApiToken"].MaskText.ShouldBe("••••••");
        configuration.Properties["Address.TaxNumber"].MaskText.ShouldBe("##");
        configuration.Properties["InternalNote"].Mode.ShouldBe(EntitiesDashboardPropertyMode.Hide);
        configuration.Properties["Address.Street"].Mode.ShouldBe(EntitiesDashboardPropertyMode.Hide);
        configuration.Properties["Age"].Mode.ShouldBe(EntitiesDashboardPropertyMode.Mask);
    }

    [Fact]
    public void EntityBuilder_ExpressionDeeperThanOneMember_Throws()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();

        // Act & Assert
        Should.Throw<ArgumentException>(() => sut.Entity<Customer>(e => e.Mask(c => c.Address.City.Length)));
        Should.Throw<ArgumentException>(() => sut.Entity<Customer>(e => e.Mask(c => c.Name.ToUpper())));
    }

    [Fact]
    public void EntityBuilder_Reference_RecordsTargetOrNone()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();

        // Act
        sut.Entity<Order>(e => e.Reference(o => o.RegionId).To<Region>().Reference(o => o.CustomerId).None());

        // Assert
        var references = sut.EntityConfigurations[typeof(Order)].References;
        references["RegionId"].ShouldBe(typeof(Region));
        references.ContainsKey("CustomerId").ShouldBeTrue();
        references["CustomerId"].ShouldBeNull();
    }

    [Fact]
    public void Validator_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();
        sut.Entity<Customer>(e => e.Mask(c => c.ApiToken).Hide(c => c.Address.Street));

        // Act & Assert
        Should.NotThrow(() => EntitiesDashboardConfigurationValidator.Validate(sut));
    }

    [Theory]
    [InlineData(0, 200, 5000, 100, "BatchSize")]
    [InlineData(50, 0, 5000, 100, "MaxBatchSize")]
    [InlineData(50, 200, 0, 100, "MaxExportRows")]
    [InlineData(50, 200, 5000, -1, "MaxBulkSelection")]
    public void Validator_LimitBelowOne_ThrowsNamingTheLimit(int batch, int maxBatch, int maxExport, int maxBulk, string expected)
    {
        // Arrange
        var sut = new EntitiesDashboardOptions { BatchSize = batch, MaxBatchSize = maxBatch, MaxExportRows = maxExport, MaxBulkSelection = maxBulk };

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => EntitiesDashboardConfigurationValidator.Validate(sut));

        // Assert
        exception.Message.ShouldContain("Invalid EntitiesDashboard configuration");
        exception.Message.ShouldContain(expected);
    }

    [Fact]
    public void Validator_RulesOnIdUnknownPropertyAndBadMember_AreAllReportedTogether()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();
        sut.Entity<Customer>(e => e.Hide("Id").Hide("Missing").Hide("Address.Missing").Hide("Name.Length").Hide("A.B.C"));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => EntitiesDashboardConfigurationValidator.Validate(sut));

        // Assert
        exception.Message.ShouldContain("Id property cannot be masked or hidden");
        exception.Message.ShouldContain("'Missing' is not a public instance property");
        exception.Message.ShouldContain("Address.Missing");
        exception.Message.ShouldContain("Name.Length");
        exception.Message.ShouldContain("A.B.C");
    }

    [Fact]
    public void Validator_ReferenceOnUnknownProperty_Throws()
    {
        // Arrange
        var sut = new EntitiesDashboardOptions();
        sut.Entity<Order>(e => e.Reference(o => o.RegionId).To<Region>());
        sut.EntityConfigurations[typeof(Order)].References["Nope"] = typeof(Region);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => EntitiesDashboardConfigurationValidator.Validate(sut));

        // Assert
        exception.Message.ShouldContain("reference 'Nope'");
    }

    [Fact]
    public void AddEntitiesDashboard_InvalidConfiguration_FailsFastAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => services.AddEntitiesDashboard(o => o.BatchSize = 0));
    }

    [Fact]
    public void AddEntitiesDashboard_CalledTwice_KeepsTheLastOptionsAndASingleCatalog()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRepository<Region>();

        // Act
        services.AddEntitiesDashboard(o => o.BatchSize = 10);
        services.AddEntitiesDashboard(o => o.BatchSize = 20);
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<EntitiesDashboardOptions>().BatchSize.ShouldBe(20);
        services.Count(d => d.ServiceType == typeof(EntitiesDashboardCatalog)).ShouldBe(1);
    }
}

[UnitTest("Presentation")]
public class EntitiesDashboardAccessEvaluatorTests
{
    private static ClaimsPrincipal User(bool authenticated = true, params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();

        return new ClaimsPrincipal(authenticated ? new ClaimsIdentity(claims, "test") : new ClaimsIdentity());
    }

    private static EntitiesDashboardAccessEvaluator Create(EntitiesDashboardOptions options, IAuthorizationService authorization = null)
    {
        var services = new ServiceCollection();
        if (authorization != null)
        {
            services.AddSingleton(authorization);
        }

        return new EntitiesDashboardAccessEvaluator(options, services.BuildServiceProvider());
    }

    [Fact]
    public async Task CanEditAsync_NothingConfigured_DeniesEvenAdmins()
    {
        // Arrange
        var sut = Create(new EntitiesDashboardOptions());

        // Act
        var result = await sut.CanEditAsync(User(true, "admin"));

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CanEditAsync_UnauthenticatedUser_Denies()
    {
        // Arrange
        var sut = Create(new EntitiesDashboardOptions { EditRoles = ["admin"] });

        // Act & Assert
        (await sut.CanEditAsync(User(false))).ShouldBeFalse();
        (await sut.CanEditAsync(null)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanEditAsync_Roles_AllowsAnyMatchingRoleOnly()
    {
        // Arrange
        var sut = Create(new EntitiesDashboardOptions { EditRoles = ["admin", "editor"] });

        // Act & Assert
        (await sut.CanEditAsync(User(true, "editor"))).ShouldBeTrue();
        (await sut.CanEditAsync(User(true, "viewer"))).ShouldBeFalse();
    }

    [Fact]
    public async Task CanEditAsync_PolicyWithoutAuthorizationService_Denies()
    {
        // Arrange
        var sut = Create(new EntitiesDashboardOptions { EditPolicy = "Editors" });

        // Act
        var result = await sut.CanEditAsync(User());

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CanEditAsync_Policy_FollowsTheAuthorizationResult(bool succeeds, bool expected)
    {
        // Arrange
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), "Editors")
            .Returns(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var sut = Create(new EntitiesDashboardOptions { EditPolicy = "Editors" }, authorization);

        // Act
        var result = await sut.CanEditAsync(User());

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task CanEditAsync_PolicyAndRoles_AreCombinedWithAnd()
    {
        // Arrange
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), "Editors").Returns(AuthorizationResult.Success());
        var sut = Create(new EntitiesDashboardOptions { EditPolicy = "Editors", EditRoles = ["admin"] }, authorization);

        // Act & Assert
        (await sut.CanEditAsync(User(true, "admin"))).ShouldBeTrue();
        (await sut.CanEditAsync(User(true, "viewer"))).ShouldBeFalse();
    }
}
