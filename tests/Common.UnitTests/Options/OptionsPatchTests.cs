// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Options;

using Microsoft.Extensions.Configuration;

/// <summary>Verifies explicit option composition independently of any feature.</summary>
[UnitTest("Common")]
public sealed class OptionsPatchTests
{
    /// <summary>Explicit false and zero values survive composition, and recorded collections are independent snapshots.</summary>
    [Fact]
    public void Apply_ExplicitDefaultsAndMutableValues_PreservesAssignmentsAndSnapshots()
    {
        var aliases = new[] { "first", "second" };
        var sut = new OptionsPatch();
        sut.Set("Enabled", false);
        sut.Set("Retry.Attempts", 0);
        sut.Set<string>("Title", null);
        sut.Set("Aliases", aliases);
        aliases[0] = "changed";
        var provenance = new Dictionary<string, string>();

        var result = sut.Apply(new ReportOptions(), "Module", provenance);

        result.Enabled.ShouldBeFalse();
        result.Title.ShouldBeNull();
        result.Retry.Attempts.ShouldBe(0);
        result.Retry.Delay.ShouldBe(TimeSpan.FromSeconds(3));
        result.Aliases.ShouldBe(["first", "second"]);
        provenance["Retry.Attempts"].ShouldBe("Module");
        provenance.ShouldNotContainKey("Retry.Delay");
    }

    /// <summary>Property names are case-insensitive and later exact assignments replace earlier ones.</summary>
    [Fact]
    public void Apply_DifferentPathCase_UsesLastAssignment()
    {
        var sut = new OptionsPatch();
        sut.Set("Retry.Attempts", 1);
        sut.Set("retry.attempts", 4);

        var result = sut.Apply(new ReportOptions(), "Module", new Dictionary<string, string>());

        sut.Contains("RETRY.ATTEMPTS").ShouldBeTrue();
        result.Retry.Attempts.ShouldBe(4);
    }

    /// <summary>Nested patches copy their assignments and extraction does not share later mutations.</summary>
    [Fact]
    public void MergeAndExtract_NestedPatch_CopiesOnlyMatchingDescendants()
    {
        var child = new OptionsPatch();
        child.Set("Attempts", 2);
        var sut = new OptionsPatch();
        sut.Merge("Retry", child);
        sut.Set("Enabled", false);
        child.Set("Attempts", 7);

        var extracted = sut.Extract("retry");
        extracted.Set("Attempts", 5);
        var result = sut.Apply(new ReportOptions(), "Module", new Dictionary<string, string>());

        result.Retry.Attempts.ShouldBe(2);
        extracted.Contains("Enabled").ShouldBeFalse();
        extracted.Apply(new RetryOptions(), "Child", new Dictionary<string, string>()).Attempts.ShouldBe(5);
    }

    /// <summary>Aliases retain insertion order and case-insensitive duplicates are omitted.</summary>
    [Fact]
    public void AppendAlias_RepeatedCase_PreservesUniqueOrder()
    {
        var sut = new OptionsPatch();
        sut.AppendAlias("Aliases", "trace");
        sut.AppendAlias("Aliases", "retry");
        sut.AppendAlias("Aliases", "TRACE");

        var result = sut.Apply(new ReportOptions(), "Module", new Dictionary<string, string>());

        result.Aliases.ShouldBe(["trace", "retry"]);
    }

    /// <summary>Typed builders accept nested property paths and reject computed expressions.</summary>
    [Fact]
    public void With_NestedProperty_RecordsOnlyTheSelectedPath()
    {
        var sut = new ReportOptionsBuilder().With(value => value.Retry.Attempts, 0);

        var result = sut.Patch.Apply(new ReportOptions(), "Module", new Dictionary<string, string>());

        result.Retry.Attempts.ShouldBe(0);
        sut.Patch.Contains("Enabled").ShouldBeFalse();
        Should.Throw<ArgumentException>(() => sut.With(value => value.Retry.Attempts + 1, 2));
    }

    /// <summary>Configuration replaces complete lists and dictionaries while preserving absent defaults and the original data.</summary>
    [Fact]
    public void ApplyOverlay_PresentSettings_ReplacesCollectionsAndRecordsProvenance()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Enabled"] = "false",
            ["Retry:Attempts"] = "0",
            ["Aliases:0"] = "replacement",
            ["Labels:new"] = "value"
        }).Build();
        var original = new ReportOptions();
        var provenance = new Dictionary<string, string>();

        var result = OptionsConfigurationOverlay.Apply(original, configuration, provenance);

        result.Enabled.ShouldBeFalse();
        result.Retry.Attempts.ShouldBe(0);
        result.Retry.Delay.ShouldBe(TimeSpan.FromSeconds(3));
        result.Aliases.ShouldBe(["replacement"]);
        result.Labels.Count.ShouldBe(1);
        result.Labels["new"].ShouldBe("value");
        original.Enabled.ShouldBeTrue();
        original.Aliases.ShouldBe(["default", "second"]);
        original.Labels.ShouldContainKey("default");
        provenance["Aliases"].ShouldBe("Configuration");
        provenance["Retry.Attempts"].ShouldBe("Configuration");
        provenance.ShouldNotContainKey("Retry.Delay");
    }

    /// <summary>Explicit empty collection sections clear concrete lists, read-only list views, arrays, sets, and dictionaries.</summary>
    [Fact]
    public void ApplyOverlay_EmptyCollections_ClearsSupportedShapes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Aliases"] = null,
            ["ReadOnlyAliases"] = null,
            ["ArrayAliases"] = null,
            ["Tags"] = null,
            ["Labels"] = null
        }).Build();
        var original = new ReportOptions();

        var result = OptionsConfigurationOverlay.Apply(original, configuration, new Dictionary<string, string>());

        result.Aliases.ShouldBeEmpty();
        result.ReadOnlyAliases.ShouldBeEmpty();
        result.ArrayAliases.ShouldBeEmpty();
        result.Tags.ShouldBeEmpty();
        result.Labels.ShouldBeEmpty();
        original.Aliases.ShouldNotBeEmpty();
        original.Labels.ShouldNotBeEmpty();
    }

    /// <summary>Configuration provider precedence remains owned by the supplied configuration tree.</summary>
    [Fact]
    public void ApplyOverlay_MultipleProviders_UsesFinalProviderValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Enabled"] = "false" })
            .AddInMemoryCollection(new Dictionary<string, string> { ["Enabled"] = "true" })
            .Build();

        var result = OptionsConfigurationOverlay.Apply(new ReportOptions(), configuration, new Dictionary<string, string>());

        result.Enabled.ShouldBeTrue();
    }

    /// <summary>Unknown settings fail without exposing their configuration values.</summary>
    [Fact]
    public void ApplyOverlay_UnknownProperty_RejectsWithoutEchoingValue()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Typo"] = "private-value"
        }).Build();

        var error = Should.Throw<ArgumentException>(() => OptionsConfigurationOverlay.Apply(new ReportOptions(), configuration,
            new Dictionary<string, string>()));

        error.Message.ShouldContain("Typo");
        error.Message.ShouldNotContain("private-value");
    }

    /// <summary>Provides serializable report configuration for composition tests.</summary>
    public sealed class ReportOptions
    {
        /// <summary>Gets or sets whether reports run.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the optional report title.</summary>
        public string Title { get; set; } = "Quarterly report";

        /// <summary>Gets or sets retry defaults.</summary>
        public RetryOptions Retry { get; set; } = new();

        /// <summary>Gets or sets the ordered aliases.</summary>
        public List<string> Aliases { get; set; } = ["default", "second"];

        /// <summary>Gets or sets aliases exposed through a read-only collection contract.</summary>
        public IReadOnlyList<string> ReadOnlyAliases { get; set; } = ["default"];

        /// <summary>Gets or sets array-based aliases.</summary>
        public string[] ArrayAliases { get; set; } = ["default"];

        /// <summary>Gets or sets unordered tags.</summary>
        public HashSet<string> Tags { get; set; } = ["default"];

        /// <summary>Gets or sets named report labels.</summary>
        public Dictionary<string, string> Labels { get; set; } = new() { ["default"] = "value" };
    }

    /// <summary>Provides retry configuration for composition tests.</summary>
    public sealed class RetryOptions
    {
        /// <summary>Gets or sets the number of attempts.</summary>
        public int Attempts { get; set; } = 3;

        /// <summary>Gets or sets the delay between attempts.</summary>
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(3);
    }

    private sealed class ReportOptionsBuilder : PatchOptionsBuilder<ReportOptions, ReportOptionsBuilder>;
}