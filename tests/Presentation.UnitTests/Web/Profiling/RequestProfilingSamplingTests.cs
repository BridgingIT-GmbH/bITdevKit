// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies anchored matching, canonical keys, validated sampling and singleton setup.</summary>
/// <example>Executed by the Presentation profiling test suite.</example>
public sealed class RequestProfilingSamplingTests
{
    /// <summary>Checks complete-path anchoring, segment wildcards and case-insensitive exclusions.</summary>
    [Theory]
    [InlineData("/health", "/health/", true)]
    [InlineData("/health", "/health/live", false)]
    [InlineData("/health/**", "/HEALTH", true)]
    [InlineData("/health/**", "/health/a/b/", true)]
    [InlineData("/health/**", "/healthcheck", false)]
    [InlineData("/api/internal/*", "/api/internal/status", true)]
    [InlineData("/api/internal/*", "/api/internal/jobs/42", false)]
    [InlineData("/**/ready", "/ready", true)]
    [InlineData("/**/ready", "/a/b/ready", true)]
    [InlineData("/", "/", true)]
    [InlineData("/**", "/", true)]
    [InlineData("/a*b", "/acccb", true)]
    [InlineData("/a*b", "/acccbx", false)]
    public void Matcher_AnchoredPatterns_MatchesOnlySupportedPaths(string pattern, string path, bool expected)
    {
        var sut = new RequestProfilingPathMatcher([pattern]);
        sut.IsMatch(path).ShouldBe(expected);
    }

    /// <summary>Checks unsupported pattern syntax fails setup.</summary>
    [Theory]
    [InlineData("health")]
    [InlineData("/health?x=1")]
    [InlineData("/health/**suffix")]
    [InlineData("/health/{id}")]
    [InlineData("/[a-z]")]
    public void Matcher_InvalidPatterns_RejectsAtSetup(string pattern)
    {
        Should.Throw<ArgumentException>(() => new RequestProfilingPathMatcher([pattern]));
    }

    /// <summary>Checks prefix removal happens once on a boundary with original path casing.</summary>
    [Theory]
    [InlineData("/api/products", "/API/", "/products")]
    [InlineData("/api", "/api", "/")]
    [InlineData("/apiary/products", "/api", "/apiary/products")]
    [InlineData("/api/products", "/", "/api/products")]
    [InlineData("", "", "/")]
    public void Key_PrefixBoundary_PreservesExpectedGrouping(string path, string prefix, string expected)
    {
        RequestProfilingPathMatcher.CreateKey(path, prefix, 128).ShouldBe((expected, false));
    }

    /// <summary>Checks overlong case-equivalent keys hash identically while distinct tails remain distinct.</summary>
    [Fact]
    public void Key_LongPaths_UsesCanonicalDistinctBoundedHash()
    {
        var path = "/" + new string('a', 200);
        var first = RequestProfilingPathMatcher.CreateKey(path, "", 64);
        first.Shortened.ShouldBeTrue();
        first.Key.Length.ShouldBe(64);
        first.Key.ShouldBe(RequestProfilingPathMatcher.CreateKey(path.ToUpperInvariant(), "", 64).Key, StringCompareShould.IgnoreCase);
        first.Key.ShouldNotBe(RequestProfilingPathMatcher.CreateKey(path + "b", "", 64).Key);
    }

    /// <summary>Checks independent probability decisions with controlled random input.</summary>
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0.999, true)]
    [InlineData(0.5, 0.49, true)]
    [InlineData(0.5, 0.5, false)]
    public void Probability_ControlledInput_UsesHalfOpenThreshold(double probability, double value, bool selected)
    {
        var random = Substitute.For<IRequestProfilingRandomSource>();
        random.NextDouble().Returns(value);
        var sut = new ProbabilityRequestProfilingSamplingStrategy(probability, random);
        var decision = sut.Decide(null);
        decision.Capture.ShouldBe(selected);
        decision.InclusionProbability.ShouldBe(probability);
    }

    /// <summary>Checks invalid built-in probabilities fail setup.</summary>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Probability_InvalidSettings_RejectsAtSetup(double value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ProbabilityRequestProfilingSamplingStrategy(value));

    /// <summary>Checks one monotonic budget is shared by concurrent calls, with burst and refill.</summary>
    [Fact]
    public void RateLimit_ConcurrentSelections_RespectsBurstAndMonotonicRefill()
    {
        var clock = new SamplingClock();
        var sut = new RateLimitRequestProfilingSamplingStrategy(10, 20, clock);
        var selected = 0;
        Parallel.For(0, 100, _ => { if (sut.Decide(null).Capture) { Interlocked.Increment(ref selected); } });
        selected.ShouldBe(20);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Enumerable.Range(0, 10).Count(_ => sut.Decide(null).Capture).ShouldBe(5);
        sut.Decide(null).InclusionProbability.ShouldBeNull();
    }

    /// <summary>Checks registration composes in either order and retains an explicit disable value.</summary>
    [Fact]
    public void Setup_RepeatedAndReordered_ComposesOnlyExplicitSettings()
    {
        var services = new ServiceCollection();
        var sut = services.AddProfiling(o => o.Enabled()).WithRequestProfiling(o => o.Enabled(false).StripPathPrefix("/api"));
        sut.WithRequestProfiling().WithOperationProfiling().WithRequestProfiling(o => o.WithSampling(s => s.Probability(0.1)));
        sut.Options.Validate();
        sut.Options.Requests.Enabled.ShouldBeFalse();
        services.Count(d => d.ServiceType == typeof(RequestProfilingOptions)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RequestProfilingOptions>();
        options.StripPathPrefix.ShouldBe("/api");
        options.ConfigurationKey.ShouldBe("p=0.1");
    }

    /// <summary>Checks HTTP capture does not silently enable operations.</summary>
    [Fact]
    public void Setup_RequestsWithoutOperations_FailsFinalValidation()
    {
        var sut = new ServiceCollection().AddProfiling(o => o.Enabled()).WithRequestProfiling();
        Should.Throw<InvalidOperationException>(sut.Options.Validate);
    }

    /// <summary>Checks custom samplers cannot accidentally capture scoped services.</summary>
    [Fact]
    public void Setup_ScopedCustomSampler_RejectsRegistration()
    {
        var services = new ServiceCollection();
        services.AddScoped<AllRequestsProfilingSamplingStrategy>();
        var sut = services.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        Should.Throw<InvalidOperationException>(() => sut.WithRequestProfiling(o => o.WithSampling(s => s.UseStrategy<AllRequestsProfilingSamplingStrategy>())));
    }

    private sealed class SamplingClock : TimeProvider
    {
        private long ticks;
        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <inheritdoc />
        public override long GetTimestamp() => this.ticks;
        /// <summary>Advances controlled monotonic test time.</summary>
        public void Advance(TimeSpan duration) => this.ticks += duration.Ticks;
    }
}
