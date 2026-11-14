// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Net;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Exercises the startup-populated memory collection through the real application composition.</summary>
/// <param name="factory">The isolated host with an owned SQL Server catalog.</param>
/// <example><code>dotnet test --filter FullyQualifiedName~CoreCacheSeederTaskTests</code></example>
[Collection("DoFiesta")]
public sealed class CoreCacheSeederTaskTests(DoFiestaApplicationFactory factory)
{
    /// <summary>Retains bounded varied payloads and searchable dimensions without adding entries on a second run.</summary>
    [Fact]
    public async Task ExecuteAsync_RepeatedExecution_RetainsBoundedMemoryDataset()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var sut = ActivatorUtilities.CreateInstance<CoreCacheSeederTask>(scope.ServiceProvider);
        var catalog = factory.Services.GetRequiredService<CacheCatalog>();
        var binding = catalog.Collections[CoreCacheSeederTask.CollectionName];
        var provider = factory.Services.GetRequiredService<CacheProviderRegistry>().Get(binding.Provider.Name);
        var collection = factory.Services.GetRequiredService<IApplicationCache>().GetCollection<string>(CoreCacheSeederTask.CollectionName);

        // Act
        await sut.ExecuteAsync(CancellationToken.None);
        var first = await provider.QueryAsync(new(catalog.Options.Namespace, binding.Name, CacheSelection.Pattern("*"), DateTimeOffset.UtcNow));
        await sut.ExecuteAsync(CancellationToken.None);
        var second = await provider.QueryAsync(new(catalog.Options.Namespace, binding.Name, CacheSelection.Pattern("*"), DateTimeOffset.UtcNow));
        var largest = await collection.GetAsync("demo:memory:1048576:00");

        // Assert
        binding.Provider.Name.ShouldBe("DEMO-MEMORY");
        binding.Provider.Options.Kind.ShouldBe("InMemory");
        binding.Provider.Capabilities.Persistent.ShouldBeFalse();
        binding.Provider.Options.MaxResidentBytes.ShouldBe(32 * CacheDefaults.MiB);
        binding.Options.Contract.ShouldBe(CoreCacheSeederTask.CollectionName.ToUpperInvariant());
        binding.Options.Codec.ToUpperInvariant().ShouldBe("JSON");
        binding.Options.Compression.Enabled.ShouldBeFalse();
        binding.Options.HotEntries.Enabled.ShouldBeFalse();
        binding.Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromHours(1));
        binding.Options.Loading.CoordinationScope.ShouldBe(CacheCoordinationScope.NodeLocal);
        binding.Options.Limits.MaxEntries.ShouldBe(128);
        binding.Options.Limits.MaxResidentBytes.ShouldBe(32 * CacheDefaults.MiB);
        binding.Options.Limits.MaxEncodedBytes.ShouldBe(2 * CacheDefaults.MiB);
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        first.Value.Entries.Count.ShouldBe(48);
        second.Value.Entries.Count.ShouldBe(48);
        second.Value.Continuation.ShouldBeNull();
        second.Value.Entries.Select(entry => entry.Identity.Key).Order().ShouldBe(first.Value.Entries.Select(entry => entry.Identity.Key).Order());
        var sizes = second.Value.Entries.GroupBy(entry => entry.Dimensions.Single(dimension => dimension.Name == "PAYLOADBYTES").Values.Single().CanonicalValue).ToArray();
        sizes.Select(group => long.Parse(group.Key, System.Globalization.CultureInfo.InvariantCulture)).Order().ShouldBe(new long[] { 1024, 8192, 32768, 131072, 524288, 1048576 });
        sizes.ShouldAllBe(group => group.Count() == 8);
        second.Value.Entries.ShouldAllBe(entry => entry.Dimensions.Count == 5);
        second.Value.Entries.ShouldAllBe(entry => entry.Dimensions.Any(dimension => dimension.Name == "TAGS" && dimension.Form == CacheDimensionForm.Set && dimension.Values.Contains(CacheDimensionValue.From("demo"))));
        second.Value.Entries.Count(entry => entry.DisplayKey.Length > 80).ShouldBe(6);
        largest.IsSuccess.ShouldBeTrue();
        largest.Value.Found.ShouldBeTrue();
        largest.Value.Value.Length.ShouldBe((int)CacheDefaults.MiB);
        factory.Services.GetRequiredService<CacheRuntimeResources>().ForCollection(binding.Name).Resident.Used.ShouldBeGreaterThan(13 * CacheDefaults.MiB);
    }

    /// <summary>Filters the seeded entries by an Integer dimension and exposes their metadata to an administrator.</summary>
    [Fact]
    public async Task Entries_SeededMemoryCollection_FiltersByPayloadDimension()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<CoreCacheSeederTask>(scope.ServiceProvider).ExecuteAsync(CancellationToken.None);
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        sut.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        sut.DefaultRequestHeaders.Add("X-Test-Admin", "true");

        // Act
        using var response = await sut.GetAsync("/_bdk/dashboard/caching/collections/memory-demo/entries?prefix=demo:memory:&dimension=payloadBytes&dimensionOperator=equal&dimensionValue=1048576");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, html);
        Regex.Matches(html, "data-dashboard-row-id=").Count.ShouldBe(8);
        html.ShouldContain("demo:memory:1048576:00");
        html.ShouldNotContain("demo:memory:1024:00");
        html.ShouldContain("PAYLOADBYTES");
        html.ShouldContain("CATEGORY");
        html.ShouldContain("TAGS");
        html.ShouldNotContain("[redacted]");
        Regex.IsMatch(html, @"<[^>]+\sdata-dashboard-query-error(?:\s|=|>)").ShouldBeFalse();
    }
}