// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Gen;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

/// <summary>Exercises task-title lookups through the real cache, startup task, authenticated endpoint and dashboard.</summary>
/// <param name="factory">The isolated application host using its owned SQL Server catalog.</param>
/// <example><code>dotnet test --filter FullyQualifiedName~TaskLookupTests</code></example>
[Collection("DoFiesta")]
public sealed class TaskLookupTests(DoFiestaApplicationFactory factory)
{
    /// <summary>Warms individually indexed suggestions, reuses them and reloads the source after clearing.</summary>
    [Fact]
    public async Task ExecuteAsync_WarmedThenCleared_ReloadsCompleteTypedCatalog()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var sut = ActivatorUtilities.CreateInstance<TaskLookupSeederTask>(scope.ServiceProvider);
        var catalog = factory.Services.GetRequiredService<CacheCatalog>();
        var binding = catalog.Collections[TaskLookupCatalog.CollectionName];
        var collection = factory.Services.GetRequiredService<IApplicationCache>().GetCollection<TaskLookupModel>(TaskLookupCatalog.CollectionName);
        var lookups = factory.Services.GetRequiredService<TaskLookupCatalog>();
        var diagnostics = factory.Services.GetRequiredService<CacheDiagnostics>();
        (await collection.ClearAsync()).IsSuccess.ShouldBeTrue();
        var before = SourceCalls(diagnostics);

        // Act
        await sut.ExecuteAsync();
        var first = await lookups.GetAllAsync();
        var repeated = await lookups.GetAllAsync();
        var warmedCalls = SourceCalls(diagnostics);
        var provider = factory.Services.GetRequiredService<CacheProviderRegistry>().Get(binding.Provider.Name);
        var retained = await provider.QueryAsync(new(catalog.Options.Namespace, binding.Name, CacheSelection.Pattern("*"), DateTimeOffset.UtcNow));
        var cleared = await collection.ClearAsync();
        var reloaded = await lookups.GetAllAsync();

        // Assert
        binding.Provider.Name.ShouldBe("LOOKUPS-MEMORY");
        binding.Provider.Options.Kind.ShouldBe("InMemory");
        binding.Provider.Capabilities.Persistent.ShouldBeFalse();
        binding.Provider.Options.MaxResidentBytes.ShouldBe(8 * CacheDefaults.MiB);
        binding.Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromHours(1));
        binding.Options.Loading.CoordinationScope.ShouldBe(CacheCoordinationScope.NodeLocal);
        binding.Options.Compression.Enabled.ShouldBeFalse();
        binding.Options.HotEntries.Enabled.ShouldBeFalse();
        first.IsSuccess.ShouldBeTrue();
        repeated.IsSuccess.ShouldBeTrue();
        reloaded.IsSuccess.ShouldBeTrue();
        first.Value.Count.ShouldBe(72);
        first.Value.Select(entry => entry.Title).Distinct().Count().ShouldBe(72);
        first.Value.GroupBy(entry => entry.Category).Count().ShouldBe(6);
        first.Value.GroupBy(entry => entry.Category).ShouldAllBe(group => group.Count() == 12);
        first.Value.ShouldAllBe(entry => entry.Language == "en" && !string.IsNullOrWhiteSpace(entry.Technology));
        repeated.Value.ShouldBe(first.Value);
        reloaded.Value.ShouldBe(first.Value);
        warmedCalls.ShouldBe(before + 72);
        SourceCalls(diagnostics).ShouldBe(before + 144);
        retained.IsSuccess.ShouldBeTrue();
        retained.Value.Entries.Count.ShouldBe(72);
        retained.Value.Continuation.ShouldBeNull();
        retained.Value.Entries.ShouldAllBe(entry => entry.Dimensions.Count == 3);
        retained.Value.Entries.ShouldAllBe(entry => entry.Dimensions.Any(dimension => dimension.Name == "CATEGORY")
            && entry.Dimensions.Any(dimension => dimension.Name == "TECHNOLOGY")
            && entry.Dimensions.Any(dimension => dimension.Name == "LANGUAGE"));
        cleared.IsSuccess.ShouldBeTrue();
    }

    /// <summary>Returns typed suggestions with fresh endpoint authentication even when the cache is warm.</summary>
    [Fact]
    public async Task GetAll_WarmCatalog_AuthenticatesEveryReadAndReturnsSuggestions()
    {
        // Arrange
        (await factory.Services.GetRequiredService<TaskLookupCatalog>().GetAllAsync()).IsSuccess.ShouldBeTrue();
        var diagnostics = factory.Services.GetRequiredService<CacheDiagnostics>();
        var before = SourceCalls(diagnostics);
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        // Act
        using var denied = await sut.GetAsync("/api/core/task-lookups");
        sut.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        using var allowed = await sut.GetAsync("/api/core/task-lookups");
        var suggestions = await allowed.Content.ReadFromJsonAsync<TaskLookupModel[]>();

        // Assert
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await denied.Content.ReadAsStringAsync());
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
        allowed.Headers.CacheControl.NoStore.ShouldBeTrue();
        allowed.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        suggestions.Length.ShouldBe(72);
        suggestions.ShouldContain(new TaskLookupModel("Implement API endpoint", "Development", "ASP.NET Core", "en"));
        suggestions.ShouldContain(new TaskLookupModel("Optimize database query", "Performance", "SQL", "en"));
        SourceCalls(diagnostics).ShouldBe(before);
    }

    /// <summary>Deserializes the endpoint through the same generated client used by the task-creation form.</summary>
    [Fact]
    public async Task GetAll_GeneratedClient_ReturnsUsableTitleSuggestions()
    {
        // Arrange
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        using var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: client)
        {
            BaseUrl = client.BaseAddress.AbsoluteUri.TrimEnd('/')
        };
        var sut = new BackendApiClient(adapter);

        // Act
        var suggestions = await sut.Api.Core.TaskLookups.GetAsync();

        // Assert
        suggestions.Count.ShouldBe(72);
        suggestions.ShouldContain(entry => entry.Title == "Review pull request" && entry.Category == "Development"
            && entry.Technology == "General" && entry.Language == "en");
        suggestions.ShouldAllBe(entry => !string.IsNullOrWhiteSpace(entry.Title));
    }

    /// <summary>Shows the cached reference entries and limits browsing to a selected activity area.</summary>
    [Fact]
    public async Task Entries_CategorySelection_ShowsOnlyMatchingLookupEntries()
    {
        // Arrange
        (await factory.Services.GetRequiredService<TaskLookupCatalog>().GetAllAsync()).IsSuccess.ShouldBeTrue();
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        sut.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        sut.DefaultRequestHeaders.Add("X-Test-Admin", "true");

        // Act
        using var response = await sut.GetAsync("/_bdk/dashboard/caching/collections/task-lookups/entries?dimension=category&dimensionOperator=equal&dimensionValue=Security");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, html);
        Regex.Matches(html, "data-dashboard-row-id=").Count.ShouldBe(12);
        html.ShouldContain("task-title:en:Security:Review-endpoint-permissions");
        html.ShouldNotContain("task-title:en:Development:Implement-API-endpoint");
        html.ShouldContain("TECHNOLOGY");
        html.ShouldContain("LANGUAGE");
        html.ShouldNotContain("[redacted]");
        Regex.IsMatch(html, @"<[^>]+\sdata-dashboard-query-error(?:\s|=|>)").ShouldBeFalse();
    }

    private static long SourceCalls(CacheDiagnostics diagnostics) => diagnostics.ForCollection(TaskLookupCatalog.CollectionName)
        .Snapshot().Counters.GetValueOrDefault(CacheObservationKind.SourceAttempt) ?? 0;
}