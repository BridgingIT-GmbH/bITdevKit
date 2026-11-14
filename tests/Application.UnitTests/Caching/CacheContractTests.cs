namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using Shouldly;
using Xunit;

public class CacheContractTests
{
    [Fact]
    public void Lookup_NullAndAbsence_AreDistinctFromMiss()
    {
        var version = CacheEntryVersion.New();
        var nullHit = new CacheLookup<string>(true, null, CacheLookupOutcome.Hit, CacheStorageOutcome.None, version: version);
        var absence = new CacheLookup<string>(false, null, CacheLookupOutcome.Hit, CacheStorageOutcome.None, isNotFound: true, version: version);
        var miss = new CacheLookup<string>(false, null, CacheLookupOutcome.Miss, CacheStorageOutcome.None);

        nullHit.Found.ShouldBeTrue();
        nullHit.IsNotFound.ShouldBeFalse();
        absence.Found.ShouldBeFalse();
        absence.IsNotFound.ShouldBeTrue();
        miss.Version.ShouldBeNull();
        Should.Throw<ArgumentException>(() => new CacheLookup<string>(true, null, CacheLookupOutcome.Hit,
            CacheStorageOutcome.None, isNotFound: true));
    }

    [Theory]
    [InlineData(CacheStorageOutcome.Suppressed)]
    [InlineData(CacheStorageOutcome.Rejected)]
    [InlineData(CacheStorageOutcome.Failed)]
    [InlineData(CacheStorageOutcome.Indeterminate)]
    [InlineData(CacheStorageOutcome.Conflict)]
    public void Lookup_UnretainedResult_CannotClaimVersion(CacheStorageOutcome storage)
    {
        Should.Throw<ArgumentException>(() => new CacheLookup<string>(true, "value", CacheLookupOutcome.Loaded,
            storage, version: CacheEntryVersion.New()));
    }

    [Fact]
    public void LoadResult_ConfirmedAbsence_IsExplicit()
    {
        new CacheLoadResult<string>(null).IsNotFound.ShouldBeFalse();
        CacheLoadResult<string>.NotFound().IsNotFound.ShouldBeTrue();
        CacheLoadResult<string>.NotFound().Descriptor.Priority.ShouldBe(CacheEvictionPriority.Low);
        new CacheBinaryLoadResult().IsNotFound.ShouldBeFalse();
        CacheBinaryLoadResult.NotFound().IsNotFound.ShouldBeTrue();
    }

    [Fact]
    public void Item_CapturesOwnerDimensions()
    {
        var dimensions = new List<CacheDimension> { CacheDimension.Scalar("region", "west") };
        var sut = new CacheItem("Product:42", dimensions);
        dimensions.Clear();

        sut.Key.ShouldBe("Product:42");
        sut.Dimensions.Count.ShouldBe(1);
    }

    [Fact]
    public void Core_ReferencesOnlyCommonAndMicrosoftPackages()
    {
        typeof(IApplicationCache).Assembly.GetReferencedAssemblies()
            .Where(a => a.Name.StartsWith("BridgingIT.DevKit.", StringComparison.Ordinal))
            .All(a => a.Name.StartsWith("BridgingIT.DevKit.Common.", StringComparison.Ordinal)).ShouldBeTrue();
        typeof(ValueCodecRegistry).Assembly.GetReferencedAssemblies()
            .Any(a => a.Name.Contains("Caching", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public void PersistedMetadata_RoundTripsWithoutClrTypeActivation()
    {
        var sut = new CacheEntryMetadata
        {
            Identity = new("app", "cards", "v1", "product:42"),
            Version = CacheEntryVersion.New(),
            Dimensions = [
                CacheDimension.Set("regions", CacheDimensionKind.String, "west", "WEST", null),
                CacheDimension.Interval("ticks", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddTicks(1))],
            PublishedAt = DateTimeOffset.UtcNow,
            EncodedSizeBytes = 123
        };

        var restored = JsonSerializer.Deserialize<CacheEntryMetadata>(JsonSerializer.Serialize(sut));
        restored.Identity.ShouldBe(sut.Identity);
        restored.Version.ShouldBe(sut.Version);
        restored.Dimensions[0].Values.Count.ShouldBe(2);
        restored.Dimensions[1].Values[1].CanonicalValue.ShouldBe(sut.Dimensions[1].Values[1].CanonicalValue);
    }
}