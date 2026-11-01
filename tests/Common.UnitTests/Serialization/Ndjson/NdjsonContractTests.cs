// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text.Json;

public class NdjsonContractTests
{
    [Fact]
    public void Defaults_NewOptions_AreStrictAndBounded()
    {
        var writer = new NdjsonWriterOptions();
        var reader = new NdjsonReaderOptions();
        writer.MaxRecordBytes.ShouldBe(1_048_576);
        writer.CleanupTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        writer.ObserverTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        reader.AllowUnterminatedFinalRecord.ShouldBeFalse();
        reader.SkipEmptyLines.ShouldBeFalse();
        writer.Validate();
        reader.Validate();
    }

    [Fact]
    public void Validate_HalfConfiguredBatch_RejectsOptions()
    {
        Should.Throw<ArgumentException>(() => new NdjsonWriterOptions { BatchByteLimit = 16 }.Validate());
        Should.Throw<ArgumentException>(() => new NdjsonWriterOptions { BatchMaxDelay = TimeSpan.FromSeconds(1) }.Validate());
        Should.Throw<ArgumentOutOfRangeException>(() => new NdjsonWriterOptions { CleanupTimeout = Timeout.InfiniteTimeSpan }.Validate());
        Should.Throw<ArgumentOutOfRangeException>(() => new NdjsonReaderOptions { MaxRecordBytes = 0 }.Validate());
    }

    [Fact]
    public async Task EncodeRecordAsync_HostIndentedAndUpperCase_UsesFixedEnvelopeStructure()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        var writer = new NdjsonWriter(options);
        using var frame = await writer.EncodeRecordAsync(new NdjsonEnvelope<string> { Kind = NdjsonEnvelopeKind.Data, Data = null });
        Encoding.UTF8.GetString(frame.FramedBytes.Span).ShouldBe("{\"version\":1,\"kind\":\"data\",\"data\":null}\n");
        options.WriteIndented.ShouldBeTrue();
    }
}
