namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using Shouldly;
using Xunit;

public class CacheIdentityTests
{
    [Theory]
    [InlineData("product:ABC", "PRODUCT:abc")]
    [InlineData("é", "É")]
    [InlineData("Σ", "ς")]
    [InlineData("σ", "Σ")]
    [InlineData("ı", "I")]
    [InlineData("İ", "i")]
    [InlineData("ß", "SS")]
    [InlineData("K", "k")]
    [InlineData("𐐨", "𐐀")]
    [InlineData("a\u0308", "ä")]
    public void Identity_UnicodeEquivalence_MatchesOrdinalIgnoreCase(string first, string second)
    {
        var expected = StringComparer.OrdinalIgnoreCase.Equals(first, second);
        var left = new CacheEntryIdentity("app", "cards", "v1", first);
        var right = new CacheEntryIdentity("APP", "CARDS", "V1", second);

        (left == right).ShouldBe(expected);
        (left.Digest == right.Digest).ShouldBe(expected);
    }

    [Fact]
    public void Identity_ComponentBoundaries_DoNotCollide()
    {
        new CacheEntryIdentity("a", "bc", "v1", "d").Digest
            .ShouldNotBe(new CacheEntryIdentity("ab", "c", "v1", "d").Digest);
        var first = new CacheEntryIdentity("app", "cards", "v1", "42");
        var second = new CacheEntryIdentity("app", "cards", "v2", "42");

        first.ShouldNotBe(second);
        first.CollectionIdentity.ShouldBe(second.CollectionIdentity);
        JsonSerializer.Deserialize<CacheEntryIdentity>(JsonSerializer.Serialize(first)).ShouldBe(first);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad\0key")]
    public void Identity_InvalidKeys_RejectBeforeProviderAccess(string key) =>
        Should.Throw<ArgumentException>(() => new CacheEntryIdentity("app", "cards", "v1", key));

    [Fact]
    public void Identity_Bounds_AreUtf8Bytes()
    {
        Should.Throw<ArgumentException>(() => CacheIdentityNormalizer.Key("\uD800"));
        Should.Throw<ArgumentException>(() => CacheIdentityNormalizer.Key(new string('é', 2049)));
        CacheIdentityNormalizer.Key(new string('é', 2048)).Length.ShouldBe(2048);
        default(CacheEntryIdentity).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void KeyBuilder_Components_AreOrderedAndUnambiguous()
    {
        var sut = new CacheKeyBuilder().With("region", "a:3*b").With("id", 12L);
        sut.Build().ShouldBe(new CacheKeyBuilder().With("ID", 12L).With("REGION", "a:3*b").Build());
        sut.Build().ShouldNotBe(new CacheKeyBuilder().With("region", "a").With("id", "3*b:12").Build());
        Should.Throw<ArgumentException>(() => sut.With("ID", 99));
        Should.Throw<ArgumentException>(() => new CacheKeyBuilder().With("time", DateTime.Now));
    }

    [Fact]
    public void KeyBuilder_Formatting_IsCultureIndependent()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var first = new CacheKeyBuilder().With("price", 12.5m).Build();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            new CacheKeyBuilder().With("price", 12.5m).Build().ShouldBe(first);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}