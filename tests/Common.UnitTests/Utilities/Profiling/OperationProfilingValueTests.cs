// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Globalization;
using System.Text.Json;

public class OperationProfilingValueTests
{
    [Fact]
    public void TryCreate_DistinctScalarTypes_PreserveTypeAndPrecisionThroughJson()
    {
        // Arrange
        object[] sources = [9007199254740993L, 1L, 1m, 1d, "1", true];

        // Act
        var values = sources.Select(source =>
        {
            ProfilingValue.TryCreate(source, out var value).ShouldBeTrue();
            return JsonSerializer.Deserialize<ProfilingValue>(JsonSerializer.Serialize(value));
        }).ToArray();

        // Assert
        values[0].Scalar.ShouldBe("9007199254740993");
        values.Select(value => value.Type).ShouldBe([
            ProfilingValueType.Int64, ProfilingValueType.Int64, ProfilingValueType.Decimal,
            ProfilingValueType.Double, ProfilingValueType.String, ProfilingValueType.Boolean
        ]);
        values.Skip(1).Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public void TryCreate_UnsupportedOrNonUtcValues_RejectWithoutObjectRetention()
    {
        // Arrange
        object[] sources = [new object(), ulong.MaxValue, double.NaN, double.PositiveInfinity,
            float.NegativeInfinity, System.DateTime.SpecifyKind(System.DateTime.MinValue, DateTimeKind.Unspecified),
            new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(2))];

        // Act and assert
        foreach (var source in sources)
        {
            ProfilingValue.TryCreate(source, out var value).ShouldBeFalse();
            value.ShouldBeNull();
        }

        ProfilingValue.TryCreate(null, out var absent).ShouldBeFalse();
        absent.ShouldBeNull();
    }

    [Fact]
    public void ScalarConstructor_EquivalentNumericValues_CanonicalizeWithinType()
    {
        // Arrange
        var first = new ProfilingValue(ProfilingValueType.Decimal, "1.000");
        var second = new ProfilingValue(ProfilingValueType.Decimal, "1e0");

        // Act and assert
        first.ShouldBe(second);
        new ProfilingValue(ProfilingValueType.Double, "-0").ShouldBe(new ProfilingValue(ProfilingValueType.Double, "0"));
        Should.Throw<ArgumentException>(() => new ProfilingValue(ProfilingValueType.Double, "NaN"));
    }

    [Fact]
    public void ScalarConstructor_StringValues_RemainCaseSensitiveAndKeepEmptyString()
    {
        // Act and assert
        new ProfilingValue(ProfilingValueType.String, "Load").ShouldNotBe(new ProfilingValue(ProfilingValueType.String, "load"));
        ProfilingValue.TryCreate(string.Empty, out var value).ShouldBeTrue();
        value.Scalar.ShouldBe(string.Empty);
    }

    [Fact]
    public void SegmentPath_CasingAndSeparators_KeepBoundariesAndDefensiveCopy()
    {
        // Arrange
        string[] components = ["Load", "Read"];
        var path = new ProfilingSegmentPath(components);
        components[0] = "Changed";

        // Act and assert
        path.Equals(new ProfilingSegmentPath(["load", "read"])).ShouldBeTrue();
        path.Equals(new ProfilingSegmentPath(["Load/Read"])).ShouldBeFalse();
        new ProfilingSegmentPath(["a:b", "c"]).Equals(new ProfilingSegmentPath(["a", "b:c"])).ShouldBeFalse();
        path.Components[0].ShouldBe("Load");
        JsonSerializer.Deserialize<ProfilingSegmentPath>(JsonSerializer.Serialize(path)).ShouldBe(path);
    }

    [Fact]
    public void KeyComparer_CurrentCulture_DoesNotTrimOrFoldStringValues()
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            // Act and assert
            ProfilingKeyComparer.Instance.Equals("pipeline", "PIPELINE").ShouldBeTrue();
            ProfilingKeyComparer.Instance.Equals("Load", " load").ShouldBeFalse();
            ProfilingKeyComparer.Instance.GetHashCode("Load").ShouldBe(ProfilingKeyComparer.Instance.GetHashCode("load"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void OperationRecord_TimestampJson_UsesLiteralUtcSuffix()
    {
        // Arrange
        var utc = new DateTimeOffset(2026, 10, 7, 12, 30, 0, TimeSpan.Zero);
        var record = new OperationProfilingRecord { StartedUtc = utc, CompletedUtc = utc };

        // Act
        var json = JsonSerializer.Serialize(record);

        // Assert
        json.ShouldContain("2026-10-07T12:30:00.0000000Z");
        json.ShouldNotContain("+00:00");
        JsonSerializer.Deserialize<OperationProfilingRecord>(json).StartedUtc.ShouldBe(utc);
    }

    [Fact]
    public void OperationContracts_AssemblyAndFields_ContainNoWebOrApplicationPayloadTypes()
    {
        // Arrange
        var contracts = new[] { typeof(IOperationProfiler), typeof(OperationProfilingRecord), typeof(HttpRequestProfilingMetadata), typeof(ProfilingSegmentSummary) };

        // Act and assert
        contracts.ShouldAllBe(type => type.Assembly == typeof(IResult).Assembly);
        typeof(OperationProfilingRecord).GetProperties().ShouldNotContain(property => property.PropertyType == typeof(object) || typeof(Exception).IsAssignableFrom(property.PropertyType));
    }
}
