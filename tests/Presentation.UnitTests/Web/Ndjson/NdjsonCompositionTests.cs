// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Presentation.Web;

/// <summary>Checks endpoint overrides and captured NDJSON defaults.</summary>
public sealed class NdjsonCompositionTests
{
    /// <summary>Filename capture preserves exact UTF-16 text rather than normalizing it through JSON value snapshots.</summary>
    [Fact]
    public void Apply_FilenameWithUnpairedSurrogate_PreservesExactText()
    {
        var defaults = Defaults();
        var filename = "report-" + (char)0xd800 + ".ndjson";

        var sut = defaults.Apply(new() { DownloadFileName = filename });

        sut.DownloadFileName.ShouldBe(filename);
        defaults.DownloadFileName.ShouldBe("items.ndjson");
    }

    /// <summary>Omitted endpoint properties preserve every configured scalar and collection-independent default.</summary>
    [Fact]
    public void Apply_OmittedOverrides_PreservesCapturedDefaults()
    {
        var defaults = Defaults();

        var sut = defaults.Apply(new());

        sut.ShouldNotBeSameAs(defaults);
        sut.MaxRecordBytes.ShouldBe(512);
        sut.Mode.ShouldBe(NdjsonMode.Envelope);
        sut.HeartbeatInterval.ShouldBe(TimeSpan.FromSeconds(3));
        sut.WriteTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        sut.CleanupTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        sut.ObserverTimeout.ShouldBe(TimeSpan.FromSeconds(4));
        sut.MaxDuration.ShouldBe(TimeSpan.FromMinutes(2));
        sut.BatchByteLimit.ShouldBe(2048);
        sut.BatchMaxDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        sut.EnableCompression.ShouldBeTrue();
        sut.DownloadFileName.ShouldBe("items.ndjson");
    }

    /// <summary>An explicit false value overrides an enabled default without changing the source options.</summary>
    [Fact]
    public void Apply_ExplicitCompressionFalse_OverridesEnabledDefault()
    {
        var defaults = Defaults();

        var sut = defaults.Apply(new() { EnableCompression = false });

        sut.EnableCompression.ShouldBeFalse();
        defaults.EnableCompression.ShouldBeTrue();
        sut.Mode.ShouldBe(defaults.Mode);
    }

    /// <summary>Explicit duration clearing removes the inherited cap without modifying its owner.</summary>
    [Fact]
    public void Apply_ClearDuration_RemovesOnlyTheCapturedCap()
    {
        var defaults = Defaults();

        var sut = defaults.Apply(new() { ClearMaxDuration = true });

        sut.MaxDuration.ShouldBeNull();
        defaults.MaxDuration.ShouldBe(TimeSpan.FromMinutes(2));
        sut.WriteTimeout.ShouldBe(defaults.WriteTimeout);
    }

    /// <summary>A duration value and an explicit request to remove that duration remain mutually exclusive.</summary>
    [Fact]
    public void Apply_DurationOverrideAndClear_RejectsConflict()
    {
        var sut = Defaults();

        Should.Throw<ArgumentException>(() => sut.Apply(new()
        {
            ClearMaxDuration = true,
            MaxDuration = TimeSpan.FromMinutes(1)
        }));

        sut.MaxDuration.ShouldBe(TimeSpan.FromMinutes(2));
    }

    /// <summary>A complete endpoint batch pair replaces the inherited pair as one setting.</summary>
    [Fact]
    public void Apply_CompleteBatchOverride_ReplacesTheInheritedPair()
    {
        var defaults = Defaults();

        var sut = defaults.Apply(new()
        {
            BatchByteLimit = 4096,
            BatchMaxDelay = TimeSpan.FromMilliseconds(250)
        });

        sut.BatchByteLimit.ShouldBe(4096);
        sut.BatchMaxDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
        defaults.BatchByteLimit.ShouldBe(2048);
        defaults.BatchMaxDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Supplying half a batch pair cannot borrow the missing half from host defaults.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_PartialBatchOverride_RejectsInsteadOfInheritingMissingMate(bool bytesOnly)
    {
        var sut = Defaults();
        var overrides = bytesOnly
            ? new NdjsonEndpointOptions { BatchByteLimit = 4096 }
            : new NdjsonEndpointOptions { BatchMaxDelay = TimeSpan.FromMilliseconds(250) };

        Should.Throw<ArgumentException>(() => sut.Apply(overrides));

        sut.BatchByteLimit.ShouldBe(2048);
        sut.BatchMaxDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Disabling batching clears both inherited batch values in the captured result.</summary>
    [Fact]
    public void Apply_DisableBatching_ClearsBothInheritedValues()
    {
        var defaults = Defaults();

        var sut = defaults.Apply(new() { DisableBatching = true });

        sut.BatchByteLimit.ShouldBeNull();
        sut.BatchMaxDelay.ShouldBeNull();
        defaults.BatchByteLimit.ShouldBe(2048);
        defaults.BatchMaxDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Batch disable cannot be combined with either member of an endpoint batch pair.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_BatchDisableAndOverride_RejectsConflict(bool bytesOverride)
    {
        var sut = Defaults();
        var overrides = new NdjsonEndpointOptions { DisableBatching = true };
        if (bytesOverride) { overrides.BatchByteLimit = 4096; }
        else { overrides.BatchMaxDelay = TimeSpan.FromMilliseconds(250); }

        Should.Throw<ArgumentException>(() => sut.Apply(overrides));
    }

    /// <summary>Serializer converters and policies survive capture while later option mutations cannot affect that capture.</summary>
    [Fact]
    public void Apply_CustomJsonOptions_PreservesConverterAndIsolatesLaterMutation()
    {
        var defaults = Defaults();
        var serializer = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
        var converter = new LabelConverter("endpoint:");
        serializer.Converters.Add(converter);
        var overrides = new NdjsonEndpointOptions { JsonOptions = serializer };

        var sut = defaults.Apply(overrides);
        serializer.Converters.Clear();
        serializer.PropertyNamingPolicy = null;
        overrides.JsonOptions = null;

        sut.JsonOptions.ShouldNotBeSameAs(serializer);
        sut.JsonOptions.Converters.ShouldContain(converter);
        sut.JsonOptions.WriteIndented.ShouldBeFalse();
        JsonSerializer.Serialize(new Label("report"), sut.JsonOptions).ShouldBe("\"endpoint:report\"");
        JsonSerializer.Serialize(new Document("report"), sut.JsonOptions).ShouldBe("{\"display_name\":\"report\"}");
    }

    private static NdjsonStreamingOptions Defaults() => new()
    {
        MaxRecordBytes = 512,
        Mode = NdjsonMode.Envelope,
        HeartbeatInterval = TimeSpan.FromSeconds(3),
        WriteTimeout = TimeSpan.FromSeconds(7),
        CleanupTimeout = TimeSpan.FromSeconds(2),
        ObserverTimeout = TimeSpan.FromSeconds(4),
        MaxDuration = TimeSpan.FromMinutes(2),
        BatchByteLimit = 2048,
        BatchMaxDelay = TimeSpan.FromMilliseconds(100),
        EnableCompression = true,
        DownloadFileName = "items.ndjson"
    };

    private sealed record Label(string Value);

    private sealed record Document(string DisplayName);

    private sealed class LabelConverter(string prefix) : JsonConverter<Label>
    {
        public override Label Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString());

        public override void Write(Utf8JsonWriter writer, Label value, JsonSerializerOptions options) =>
            writer.WriteStringValue(prefix + value.Value);
    }
}