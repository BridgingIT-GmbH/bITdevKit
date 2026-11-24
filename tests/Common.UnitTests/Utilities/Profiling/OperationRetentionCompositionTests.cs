// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

/// <summary>Checks explicit retention assignments and independent provider-default snapshots.</summary>
public sealed class OperationRetentionCompositionTests
{
    /// <summary>An explicit assignment equal to an in-memory default still overrides a different provider default.</summary>
    [Theory]
    [InlineData("count")]
    [InlineData("bytes")]
    [InlineData("age")]
    public void SnapshotWithRetentionDefaults_ExplicitDefault_OnlyOverridesItsOwnProperty(string property)
    {
        var sut = new OperationProfilingOptions();
        switch (property)
        {
            case "count": sut.MaximumRetainedOperations = 10000; break;
            case "bytes": sut.MaximumRetainedBytes = 128L * 1024 * 1024; break;
            case "age": sut.MaximumOperationAge = TimeSpan.FromHours(24); break;
        }

        var result = sut.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));

        result.MaximumRetainedOperations.ShouldBe(property == "count" ? 10000 : 100000);
        result.MaximumRetainedBytes.ShouldBe(property == "bytes" ? 128L * 1024 * 1024 : long.MaxValue);
        result.MaximumOperationAge.ShouldBe(property == "age" ? TimeSpan.FromHours(24) : TimeSpan.FromDays(7));
        sut.MaximumRetainedOperations.ShouldBe(10000);
        sut.MaximumRetainedBytes.ShouldBe(128L * 1024 * 1024);
        sut.MaximumOperationAge.ShouldBe(TimeSpan.FromHours(24));
    }

    /// <summary>Mutating an independent snapshot cannot mark an omitted source property as explicitly configured.</summary>
    [Fact]
    public void Snapshot_LaterAssignments_KeepPresenceIndependentInBothDirections()
    {
        var sut = new OperationProfilingOptions();
        var snapshot = sut.Snapshot();
        snapshot.MaximumRetainedOperations = 25;
        sut.MaximumOperationAge = TimeSpan.FromDays(2);

        var source = sut.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));
        var captured = snapshot.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));

        source.MaximumRetainedOperations.ShouldBe(100000);
        source.MaximumOperationAge.ShouldBe(TimeSpan.FromDays(2));
        captured.MaximumRetainedOperations.ShouldBe(25);
        captured.MaximumOperationAge.ShouldBe(TimeSpan.FromDays(7));
    }

    /// <summary>Applying inherited defaults does not turn them into explicit assignments for a later provider.</summary>
    [Fact]
    public void SnapshotWithRetentionDefaults_RepeatedComposition_ReplacesOnlyInheritedValues()
    {
        var sut = new OperationProfilingOptions { MaximumRetainedOperations = 25 };
        var first = sut.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));

        var result = first.SnapshotWithRetentionDefaults(200000, 4096, TimeSpan.FromDays(14));

        result.MaximumRetainedOperations.ShouldBe(25);
        result.MaximumRetainedBytes.ShouldBe(4096);
        result.MaximumOperationAge.ShouldBe(TimeSpan.FromDays(14));
        first.MaximumRetainedBytes.ShouldBe(long.MaxValue);
        first.MaximumOperationAge.ShouldBe(TimeSpan.FromDays(7));
    }

    /// <summary>An explicitly invalid zero remains available to normal validation rather than silently inheriting a valid limit.</summary>
    [Fact]
    public void SnapshotWithRetentionDefaults_ExplicitZero_PreservesValidationFailure()
    {
        var sut = new OperationProfilingOptions { Enabled = true, MaximumRetainedOperations = 0 };

        var result = sut.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));

        result.MaximumRetainedOperations.ShouldBe(0);
        Should.Throw<InvalidOperationException>(result.Validate);
    }
}