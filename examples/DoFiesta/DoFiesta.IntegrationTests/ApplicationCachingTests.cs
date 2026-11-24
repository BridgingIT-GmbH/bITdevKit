// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Net;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Exercises the real reference-catalog cache, relational composition, wire format and authorization.</summary>
/// <param name="factory">The isolated host using an owned SQL Server catalog and real caching provider.</param>
/// <example><code>dotnet test --filter FullyQualifiedName~ApplicationCachingTests</code></example>
[Collection("DoFiesta")]
public sealed class ApplicationCachingTests(DoFiestaApplicationFactory factory)
{
    /// <summary>Returns client-compatible JSON to different authenticated users while reusing one retained source result.</summary>
    [Fact]
    public async Task Enumerations_RepeatedAuthenticatedReads_ReuseRetainedJsonWithoutChangingWireFormat()
    {
        // Arrange
        var catalog = factory.Services.GetRequiredService<EnumerationCatalog>();
        var collection = factory.Services.GetRequiredService<IApplicationCache>().GetCollection<string>(EnumerationCatalog.CollectionName);
        var removed = await collection.RemoveAsync(catalog.CacheKey);
        removed.IsSuccess.ShouldBeTrue();
        var diagnostics = factory.Services.GetRequiredService<CacheDiagnostics>();
        var before = SourceCalls(diagnostics);
        var expected = JsonSerializer.Serialize(new EnumerationModel(), JsonSerializerOptions.Web);
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        sut.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);

        // Act
        using var first = await sut.GetAsync("/api/core/enumerations");
        var firstJson = await first.Content.ReadAsStringAsync();
        sut.DefaultRequestHeaders.Remove("X-Test-User");
        sut.DefaultRequestHeaders.Add("X-Test-User", "reference-reader");
        using var second = await sut.GetAsync("/api/core/enumerations");
        var retained = await collection.GetAsync(catalog.CacheKey);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK, firstJson);
        second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        first.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        first.Headers.CacheControl.NoStore.ShouldBeTrue();
        firstJson.ShouldBe(expected);
        (await second.Content.ReadAsStringAsync()).ShouldBe(expected);
        using var document = JsonDocument.Parse(firstJson);
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        var parsed = new Microsoft.Kiota.Serialization.Json.JsonParseNode(document.RootElement)
            .GetObjectValue(BridgingIT.DevKit.Examples.DoFiesta.Presentation.Gen.Models.EnumerationModel.CreateFromDiscriminatorValue);
        parsed.TodoStatuses.ShouldNotBeNull();
        parsed.TodoStatuses.Select(status => status.Id).ShouldBe(TodoStatus.GetAll().Select(status => (int?)status.Id));
        parsed.TodoStatuses.First().Value.ShouldBe(TodoStatus.New.Value);
        parsed.TodoStatuses.First().Enabled.ShouldBe(true);
        retained.IsSuccess.ShouldBeTrue();
        retained.Value.Found.ShouldBeTrue();
        retained.Value.Version.HasValue.ShouldBeTrue();
        retained.Value.Value.ShouldBe(expected);
        SourceCalls(diagnostics).ShouldBe(before + 1);
    }

    /// <summary>A warm shared catalog never bypasses the endpoint's normal authentication requirement.</summary>
    [Fact]
    public async Task Enumerations_WarmCache_UnauthenticatedReadIsRejected()
    {
        // Arrange
        var warm = await factory.Services.GetRequiredService<EnumerationCatalog>().GetJsonAsync();
        warm.IsSuccess.ShouldBeTrue();
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        // Act
        using var result = await sut.GetAsync("/api/core/enumerations");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await result.Content.ReadAsStringAsync());
    }

    /// <summary>Uses fresh independent cache contexts and a deployed model with the configured central provider and scope.</summary>
    [Fact]
    public async Task CacheComposition_CreatesIndependentContextsAndAnnotatedSharedStore()
    {
        // Arrange
        var catalog = factory.Services.GetRequiredService<CacheCatalog>();
        var sut = factory.Services.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var first = sut.CreateDbContext();
        await using var second = sut.CreateDbContext();
        await using var scope = factory.Services.CreateAsyncScope();
        var business = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        // Act
        var binding = catalog.Collections[EnumerationCatalog.CollectionName];
        var store = await first.CacheStores.SingleAsync();

        // Assert
        first.ShouldNotBeSameAs(second);
        first.Database.GetDbConnection().ShouldNotBeSameAs(second.Database.GetDbConnection());
        first.Database.GetDbConnection().DataSource.ShouldBe(business.Database.GetDbConnection().DataSource);
        first.Database.GetDbConnection().Database.ShouldBe(business.Database.GetDbConnection().Database);
        first.Model.FindEntityType(typeof(CacheEntryEntity)).GetTableName().ShouldBe("__Caching_Entries");
        store.MetadataFormat.ShouldBe(1);
        catalog.Options.Namespace.ShouldBe("dofiesta");
        binding.Provider.Options.Kind.ShouldBe("EntityFramework");
        binding.Provider.Options.Engine.ShouldBe("SqlServer");
        binding.Options.Loading.CoordinationScope.ShouldBe(CacheCoordinationScope.SharedStore);
        binding.Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromHours(1));
        catalog.Integrations.ContainsKey("Dashboard").ShouldBeTrue();
        catalog.Integrations.ContainsKey("Endpoints").ShouldBeFalse();
    }

    /// <summary>Applies the existing administrator dashboard boundary to caching pages too.</summary>
    [Fact]
    public async Task CachingDashboard_RequiresAdministratorAndRendersForAdministrator()
    {
        // Arrange
        var catalog = factory.Services.GetRequiredService<EnumerationCatalog>();
        (await catalog.GetJsonAsync()).IsSuccess.ShouldBeTrue();
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        sut.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);

        // Act
        using var denied = await sut.GetAsync("/_bdk/dashboard/caching");
        sut.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        using var allowed = await sut.GetAsync("/_bdk/dashboard/caching");

        // Assert
        denied.StatusCode.ShouldBe(HttpStatusCode.Found, await denied.Content.ReadAsStringAsync());
        denied.Headers.Location.OriginalString.ShouldContain("access-denied");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
        (await allowed.Content.ReadAsStringAsync()).ShouldContain("Caching");
        using var entries = await sut.GetAsync("/_bdk/dashboard/caching/collections/reference-catalog/entries");
        entries.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await entries.Content.ReadAsStringAsync();
        html.ShouldContain(catalog.CacheKey);
        html.ShouldNotContain("[redacted]");
    }

    private static long SourceCalls(CacheDiagnostics diagnostics) => diagnostics.ForCollection(EnumerationCatalog.CollectionName)
        .Snapshot().Counters.GetValueOrDefault(CacheObservationKind.SourceAttempt) ?? 0;
}