namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class CacheConfigurationTests
{
    /// <summary>Rejects a default chart window outside retained history when the finalized settings come from code or configuration.</summary>
    /// <param name="fromConfiguration">Whether the inconsistent settings come from the host configuration rather than the fluent builder.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Registration_ChartWindowExceedsHistory_RejectsFinalizedCatalog(bool fromConfiguration)
    {
        var settings = fromConfiguration ? new Dictionary<string, string>
        {
            ["Diagnostics:HistoryRetention"] = "00:05:00",
            ["Diagnostics:ChartWindow"] = "00:15:00"
        } : [];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        var cache = services.AddApplicationCaching().WithConfiguration(configuration).WithValueContract<string>("text").WithCollection<string>("items");
        if (!fromConfiguration)
        {
            cache.WithOptions(options => options.With(value => value.Diagnostics.HistoryRetention, TimeSpan.FromMinutes(5))
                .With(value => value.Diagnostics.ChartWindow, TimeSpan.FromMinutes(15)));
        }

        using var sut = services.BuildServiceProvider();

        Should.Throw<ArgumentException>(() => sut.GetRequiredService<CacheCatalog>()).Message.ShouldContain("Diagnostics.ChartWindow");
    }

    /// <summary>Accepts a default chart window equal to or shorter than retained history, including the supported one-hour maximum.</summary>
    /// <param name="retentionMinutes">The configured retention in minutes.</param>
    /// <param name="windowMinutes">The configured default displayed window in minutes.</param>
    [Theory]
    [InlineData(15, 5)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    public void Registration_ChartWindowWithinHistory_FinalizesCatalog(int retentionMinutes, int windowMinutes)
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching()
            .WithOptions(options => options.With(value => value.Diagnostics.HistoryRetention, TimeSpan.FromMinutes(retentionMinutes))
                .With(value => value.Diagnostics.ChartWindow, TimeSpan.FromMinutes(windowMinutes)))
            .WithValueContract<string>("text").WithCollection<string>("items");
        using var provider = services.BuildServiceProvider();

        var sut = provider.GetRequiredService<CacheCatalog>();

        sut.Options.Diagnostics.ChartWindow.ShouldBe(TimeSpan.FromMinutes(windowMinutes));
        sut.Options.Diagnostics.HistoryRetention.ShouldBe(TimeSpan.FromMinutes(retentionMinutes));
    }

    [Fact]
    public void Overlay_ExplicitFalseZeroAndEmptyList_AreNotOmission()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "HotEntries": { "Enabled": false }, "Expiration": { "JitterRatio": 0 },
              "Behaviors": { "SourceExecution": [] } }
            """));
        var configuration = new ConfigurationBuilder().AddJsonStream(input).Build();
        var original = new CacheCollectionOptions
        {
            HotEntries = new() { Enabled = true, MaxEntries = 123 },
            Expiration = new() { JitterRatio = 0.2, AbsoluteTtl = TimeSpan.FromMinutes(7) },
            Behaviors = new() { SourceExecution = ["retry", "trace"], CollectionOperations = ["trace"] }
        };
        var provenance = new Dictionary<string, string>();

        var result = OptionsConfigurationOverlay.Apply(original, configuration, provenance);

        result.HotEntries.Enabled.ShouldBeFalse();
        result.HotEntries.MaxEntries.ShouldBe(123);
        result.Expiration.JitterRatio.ShouldBe(0);
        result.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(7));
        result.Behaviors.SourceExecution.ShouldBeEmpty();
        result.Behaviors.CollectionOperations.ShouldBe(["trace"]);
        original.HotEntries.Enabled.ShouldBeTrue();
        original.Behaviors.SourceExecution.Count.ShouldBe(2);
        provenance["HotEntries.Enabled"].ShouldBe("Configuration");
        provenance.ContainsKey("HotEntries.MaxEntries").ShouldBeFalse();
    }

    [Fact]
    public void Overlay_OrderedLists_ReplaceInsteadOfMergingByIndex()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Behaviors:SourceExecution:0"] = "new",
            ["Expiration:AbsoluteTtl"] = "00:30:00"
        }).Build();
        var original = new CacheCollectionOptions { Behaviors = new() { SourceExecution = ["first", "second"] } };

        var result = OptionsConfigurationOverlay.Apply(original, configuration, new Dictionary<string, string>());

        result.Behaviors.SourceExecution.ShouldBe(["new"]);
        result.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Overlay_HostProviderPrecedence_IsPreserved()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Enabled"] = "false" })
            .AddInMemoryCollection(new Dictionary<string, string> { ["Enabled"] = "true" })
            .Build();

        var result = OptionsConfigurationOverlay.Apply(new CacheCollectionOptions(), configuration, new Dictionary<string, string>());

        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void Overlay_UnknownSetting_FailsWithoutEchoingValue()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Typo"] = "sensitive-value"
        }).Build();

        var error = Should.Throw<ArgumentException>(() => OptionsConfigurationOverlay.Apply(new CacheCollectionOptions(),
            configuration, new Dictionary<string, string>()));

        error.Message.ShouldNotContain("sensitive-value");
    }
}