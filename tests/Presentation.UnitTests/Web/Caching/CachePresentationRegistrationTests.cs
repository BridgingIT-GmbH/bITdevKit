// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Text;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Caching;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies independent management registration and immutable, bounded public write contracts.</summary>
public sealed class CachePresentationRegistrationTests
{
    /// <summary>Module collections and later configuration contribute to the HTTP and Dashboard integrations selected once by the host.</summary>
    [Fact]
    public void Registration_ModulesShareOneManagementCatalog()
    {
        var services = new ServiceCollection();
        var host = services.AddApplicationCaching().WithEndpoints().WithDashboard();
        services.AddApplicationCaching().WithValueContract<string>("catalog-text")
            .WithCollection<string>("catalog-cards", collection => collection.UseContract("catalog-text"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Endpoints:Enabled"] = "false",
            ["Collections:orders-status:Endpoints:Enabled"] = "false"
        }).Build();
        services.AddApplicationCaching().WithConfiguration(configuration).WithValueContract<string>("orders-text")
            .WithCollection<string>("orders-status", collection => collection.UseContract("orders-text"));

        var sut = host.BuildCatalog();

        sut.Collections.Count.ShouldBe(2);
        sut.Integrations["Endpoints"]["Enabled"].ShouldBe("false");
        sut.Collections["orders-status"].Options.Extensions["Endpoints"]["Enabled"].ShouldBe("false");
        services.Count(d => d.ServiceType == typeof(IEndpoints) && d.ImplementationType == typeof(CacheEndpoints)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IDashboardPageProvider) &&
            d.ImplementationType == typeof(BridgingIT.DevKit.Presentation.Web.Caching.Dashboard.DashboardPageProvider)).ShouldBe(1);
    }

    /// <summary>Every combination enables exactly its selected HTTP, Dashboard, Console, and MCP registrations.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void Registration_AllPresentationCapabilitiesRemainIndependent(int flags)
    {
        var services = new ServiceCollection();
        var cache = services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("cards");
        if ((flags & 1) != 0) { cache.WithEndpoints(); }

        if ((flags & 2) != 0) { cache.WithDashboard(); }

        if ((flags & 4) != 0) { cache.WithConsoleCommands(); }

        if ((flags & 8) != 0) { cache.WithMcpHandlers(); }

        services.Any(d => d.ServiceType == typeof(IEndpoints) && d.ImplementationType == typeof(CacheEndpoints)).ShouldBe((flags & 1) != 0);
        services.Any(d => d.ServiceType == typeof(IDashboardPageProvider) &&
            d.ImplementationType == typeof(BridgingIT.DevKit.Presentation.Web.Caching.Dashboard.DashboardPageProvider)).ShouldBe((flags & 2) != 0);
        services.Any(d => d.ServiceType == typeof(IConsoleCommand) && d.ImplementationType == typeof(ApplicationCachingConsoleCommand)).ShouldBe((flags & 4) != 0);
        services.Any(d => d.ServiceType == typeof(IMcpHandler) && d.ImplementationType == typeof(ApplicationCachingMcpHandler)).ShouldBe((flags & 8) != 0);
    }

    /// <summary>Finalized public profiles cannot be replaced or extended after host validation.</summary>
    [Fact]
    public void WriteProfiles_FreezePreventsLateContractMutation()
    {
        var sut = new CacheHttpWriteProfiles();
        var profile = new CacheJsonHttpWriteProfile<string, string>(value => value, value => 64 + (value?.Length ?? 0) * 2L);
        sut.Add("text", profile);
        sut.Freeze();

        sut.Profiles["TEXT"].ShouldBeSameAs(profile);
        Should.Throw<InvalidOperationException>(() => sut.Add("another", profile));
    }

    /// <summary>An unknown or excessive boundary graph estimate rejects the value before its trusted mapper runs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(1025L)]
    public async Task PublicModel_UnboundedGraphNeverReachesTheMapper(long? estimate)
    {
        var mapped = false;
        var sut = new CacheJsonHttpWriteProfile<string, string>(value => { mapped = true; return value; }, _ => estimate, maximumDecodedBytes: 1024);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("\"value\""));

        var result = await sut.ReadAsync(input);

        result.IsFailure.ShouldBeTrue();
        mapped.ShouldBeFalse();
    }
}