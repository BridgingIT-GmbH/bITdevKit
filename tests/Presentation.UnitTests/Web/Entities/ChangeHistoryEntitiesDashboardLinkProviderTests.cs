// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies the link from Entities dashboard rows to the Change History dashboard.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~ChangeHistoryEntitiesDashboardLinkProviderTests</code></example>
[UnitTest("Presentation")]
public class ChangeHistoryEntitiesDashboardLinkProviderTests
{
    [Fact]
    public void GetLink_WithTrackedEntity_ReturnsChangeHistoryUrlWithRegistrationAndEntityId()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().WithGroupPath("/admin").Build();
        var sut = CreateSut(options, withDescriptor: true);
        var id = Guid.NewGuid().ToString();

        // Act
        var result = sut.GetLink(typeof(HistoryCustomer), id);

        // Assert
        var key = $"{typeof(HistoryContext).FullName}:{typeof(HistoryCustomer).FullName}";
        result.ShouldBe($"/admin/change-history?registration={Uri.EscapeDataString(key)}&entityId={Uri.EscapeDataString(id)}");
    }

    [Fact]
    public void GetLink_WithEntityIdNeedingEscaping_EscapesEntityId()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().WithGroupPath("/admin").Build();
        var sut = CreateSut(options, withDescriptor: true);

        // Act
        var result = sut.GetLink(typeof(HistoryCustomer), "a b&c");

        // Assert
        result.ShouldEndWith("&entityId=a%20b%26c");
    }

    [Fact]
    public void GetLink_WithoutDescriptorForEntityType_ReturnsNull()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var sut = CreateSut(options, withDescriptor: false);

        // Act
        var result = sut.GetLink(typeof(HistoryCustomer), "1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetLink_WithDescriptorForOtherEntityType_ReturnsNull()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var sut = CreateSut(options, withDescriptor: true);

        // Act
        var result = sut.GetLink(typeof(HistoryOrder), "1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetLink_WithChangeHistoryPageDisabled_ReturnsNull()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().DisablePages("change-history").Build();
        var sut = CreateSut(options, withDescriptor: true);

        // Act
        var result = sut.GetLink(typeof(HistoryCustomer), "1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetLink_WithDashboardDisabled_ReturnsNull()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Enabled(false).Build();
        var sut = CreateSut(options, withDescriptor: true);

        // Act
        var result = sut.GetLink(typeof(HistoryCustomer), "1");

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetLink_WithEmptyEntityId_ReturnsNull(string entityId)
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var sut = CreateSut(options, withDescriptor: true);

        // Act
        var result = sut.GetLink(typeof(HistoryCustomer), entityId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetLink_WithNullEntityType_ReturnsNull()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var sut = CreateSut(options, withDescriptor: true);

        // Act
        var result = sut.GetLink(null, "1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddChangeHistoryDashboard_RegistersLinkProviderAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChangeHistoryDashboard<HistoryTrackedEntity, HistoryContext>();

        // Assert
        var registration = services.Single(s =>
            s.ServiceType == typeof(BridgingIT.DevKit.Presentation.Web.Entities.Dashboard.IEntitiesDashboardChangeHistoryLinkProvider));
        registration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    private static ChangeHistoryEntitiesDashboardLinkProvider CreateSut(DashboardEndpointsOptions options, bool withDescriptor)
    {
        var services = new ServiceCollection();
        if (withDescriptor)
        {
            services.AddSingleton(new ChangeHistoryDashboardDescriptor(typeof(HistoryCustomer), typeof(HistoryContext), null));
        }

        return new ChangeHistoryEntitiesDashboardLinkProvider(services.BuildServiceProvider(), options);
    }

    private sealed class HistoryCustomer;

    private sealed class HistoryOrder;

    private sealed class HistoryTrackedEntity : BridgingIT.DevKit.Domain.Model.IEntity
    {
        public object Id { get; set; }
    }

    private sealed class HistoryContext(DbContextOptions<HistoryContext> options) : DbContext(options);
}
