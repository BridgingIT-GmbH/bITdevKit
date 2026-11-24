// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises named native provider composition, configuration precedence, and lazy filesystem ownership.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheFileSystemRegistrationTests</code></example>
public sealed class CacheFileSystemRegistrationTests
{
    /// <summary>Builds a native provider through fluent options without creating its root until an actual cache operation begins.</summary>
    [Fact]
    public async Task NamedProvider_DeclaredCollection_RoundTripsThroughLazyNativeStorage()
    {
        // Arrange
        var root = NewRoot();
        try
        {
            var registrations = new ServiceCollection();
            registrations.AddApplicationCaching()
                .WithFileSystemProvider("report-disk", provider => provider.Root(root)
                    .AccessKind(FileSystemStorageAccessKind.Local).Durability(FileSystemStorageDurability.Buffered)
                    .MaxResidentBytes(32 * CacheDefaults.MiB).MetadataCacheBytes(4 * CacheDefaults.MiB)
                    .MaxStorageBytes(32 * CacheDefaults.MiB).MaxEntries(128))
                .WithValueContract<string>("text")
                .WithCollection<string>("reports", collection => collection.UseContract("text").UseProvider("report-disk"));
            await using var services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var sut = services.GetRequiredService<IApplicationCache>().GetCollection<string>("reports");
            Directory.Exists(root).ShouldBeFalse();

            // Act
            var stored = await sut.SetAsync(new("quarterly"), "ready");
            var acquired = await sut.GetAsync("quarterly");

            // Assert
            stored.IsSuccess.ShouldBeTrue(string.Join(", ", stored.Errors.Select(error => error.Message)));
            stored.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
            acquired.IsSuccess.ShouldBeTrue();
            acquired.Value.Found.ShouldBeTrue();
            acquired.Value.Value.ShouldBe("ready");
            acquired.Value.Version.ShouldBe(stored.Value.Version);
            Directory.Exists(root).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, true); }
        }
    }

    /// <summary>Host configuration supplies or overrides native fluent defaults before final profile validation.</summary>
    /// <param name="configureDefaults">Whether the fluent call provides defaults that configuration must replace.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedProvider_ConfigurationOverlay_WinsWithoutFilesystemIo(bool configureDefaults)
    {
        // Arrange
        var root = NewRoot();
        var settings = new Dictionary<string, string>
        {
            ["Providers:report-disk:RootPath"] = root,
            ["Providers:report-disk:AccessKind"] = "Local",
            ["Providers:report-disk:Durability"] = "Buffered",
            ["Providers:report-disk:MaxEntries"] = "17"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var registrations = new ServiceCollection();
        var builder = registrations.AddApplicationCaching().WithConfiguration(configuration);
        if (configureDefaults)
        {
            builder.WithFileSystemProvider("report-disk", provider => provider.Root("overridden-relative-path")
                .AccessKind("overridden-profile").Durability("overridden-profile").MaxEntries(3));
        }
        else { builder.WithFileSystemProvider("report-disk"); }

        builder.WithValueContract<string>("text")
            .WithCollection<string>("reports", collection => collection.UseProvider("report-disk"));

        // Act
        var sut = builder.BuildCatalog();

        // Assert
        var selected = sut.Collections["REPORTS"].Provider;
        selected.Name.ShouldBe("REPORT-DISK");
        selected.Options.Kind.ShouldBe("FileSystem");
        selected.Options.RootPath.ShouldBe(root);
        selected.Options.AccessKind.ShouldBe("Local");
        selected.Options.Durability.ShouldBe("Buffered");
        selected.Options.MaxEntries.ShouldBe(17);
        selected.Capabilities.SharedStoreCoordination.ShouldBeTrue();
        Directory.Exists(root).ShouldBeFalse();
    }

    /// <summary>Several named native providers reuse one registered filesystem kind and retain independent root settings.</summary>
    [Fact]
    public void NamedProviders_RepeatedNativeKind_DeclareIndependentRoots()
    {
        // Arrange
        var first = NewRoot();
        var second = NewRoot();
        var registrations = new ServiceCollection();
        var builder = registrations.AddApplicationCaching().WithFileSystemProvider()
            .WithFileSystemProvider("first", provider => provider.Root(first));
        registrations.AddApplicationCaching()
            .WithFileSystemProvider("second", provider => provider.Root(second))
            .WithValueContract<string>("text")
            .WithCollection<string>("reports", collection => collection.UseProvider("first"));

        // Act
        var sut = builder.BuildCatalog();

        // Assert
        sut.Providers.Count.ShouldBe(2);
        sut.Providers["FIRST"].Options.RootPath.ShouldBe(first);
        sut.Providers["SECOND"].Options.RootPath.ShouldBe(second);
        sut.Registry.ProviderKinds.Keys.Count(kind => kind.Equals("FileSystem", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        Directory.Exists(first).ShouldBeFalse();
        Directory.Exists(second).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => builder.WithFileSystemProvider());
    }

    /// <summary>Native convenience registration cannot silently adopt a different factory registered under its stable kind alias.</summary>
    [Fact]
    public void NamedProvider_ConflictingKindFactory_RejectsBeforeFilesystemIo()
    {
        // Arrange
        var root = NewRoot();
        var registrations = new ServiceCollection();
        var sut = registrations.AddApplicationCaching().WithProviderKind("FileSystem", FileSystemCacheStoreProvider.CapabilitiesFor,
            (_, name, _) => new InMemoryCacheStoreProvider(name));

        // Act / Assert
        Should.Throw<ArgumentException>(() => sut.WithFileSystemProvider("reports", provider => provider.Root(root)));
        Directory.Exists(root).ShouldBeFalse();
    }

    /// <summary>Native convenience registration preserves the case-insensitive named-provider uniqueness contract.</summary>
    [Fact]
    public void NamedProvider_DuplicateAlias_RejectsInsteadOfReplacingItsRoot()
    {
        // Arrange
        var first = NewRoot();
        var second = NewRoot();
        var registrations = new ServiceCollection();
        var sut = registrations.AddApplicationCaching().WithFileSystemProvider("reports", provider => provider.Root(first));

        // Act / Assert
        Should.Throw<ArgumentException>(() => sut.WithFileSystemProvider("REPORTS", provider => provider.Root(second)));
        Directory.Exists(first).ShouldBeFalse();
        Directory.Exists(second).ShouldBeFalse();
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "cache-fluent-" + Guid.NewGuid().ToString("N"));
}