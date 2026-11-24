namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Shouldly;
using Xunit;

public class CachePolicyTests
{
    [Fact]
    public void Expiration_Jitter_IsSampledOnceAndOnlyShortensThePublishedLifetime()
    {
        var calls = 0;
        var options = new CacheCollectionOptions { Expiration = new() { JitterRatio = 0.2 } };

        var result = CacheExpirationPolicy.Create(options, new(), false, DateTimeOffset.UnixEpoch, () => { calls++; return 0.5; });

        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        (result.Value.ExpiresAt - result.Value.PublishedAt).ShouldBe(TimeSpan.FromMinutes(13.5));
        result.Value.SlidingIdleTimeout.ShouldBeNull();
    }

    [Fact]
    public void NegativeLifetime_IsBoundedAndDoesNotSlide()
    {
        var options = new CacheCollectionOptions { Expiration = new() { SlidingEnabled = true }, NegativeCaching = new() { Enabled = true } };

        var result = CacheExpirationPolicy.Create(options, new(), true, DateTimeOffset.UnixEpoch);

        result.IsSuccess.ShouldBeTrue();
        (result.Value.AbsoluteExpiresAt - result.Value.PublishedAt).ShouldBe(TimeSpan.FromSeconds(30));
        result.Value.SlidingIdleTimeout.ShouldBeNull();
    }

    [Fact]
    public void OwnerMetadata_LateSetsMayRepeatInDifferentOrderButCannotReplaceKnownDependencies()
    {
        var options = new CacheCollectionOptions { Dimensions = [new("labels", CacheDimensionKind.String, CacheDimensionForm.Set)] };
        var owner = new CacheItem("key", [CacheDimension.Set("labels", CacheDimensionKind.String, "a", "b")]);

        var repeated = CacheSizePolicy.MergeDimensions(owner,
            new() { Dimensions = [CacheDimension.Set("labels", CacheDimensionKind.String, "B", "A")] }, options);
        var replaced = CacheSizePolicy.MergeDimensions(owner,
            new() { Dimensions = [CacheDimension.Set("labels", CacheDimensionKind.String, "other")] }, options);

        repeated.IsSuccess.ShouldBeTrue();
        repeated.Value.Count.ShouldBe(1);
        replaced.IsFailure.ShouldBeTrue();
        replaced.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Contract);
    }
}