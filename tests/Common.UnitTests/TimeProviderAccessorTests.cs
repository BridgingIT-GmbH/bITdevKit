// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Unit tests for <see cref="TimeProviderAccessor"/> ensuring correct ambient behavior,
/// async flow, reset, and thread isolation.
/// </summary>
public class TimeProviderAccessorTests : IDisposable
{
    private readonly TimeProvider original;

    public TimeProviderAccessorTests()
    {
        // Capture original state before each test
        this.original = TimeProviderAccessor.Current;
    }

    public void Dispose()
    {
        // Always reset to avoid test pollution
        TimeProviderAccessor.Current = this.original;
    }

    /// <summary>
    /// Verifies that <see cref="TimeProviderAccessor.Current"/> returns
    /// <see cref="TimeProvider.System"/> when no value is set.
    /// </summary>
    [Fact]
    public void Current_WhenNoValueSet_ReturnsSystemProvider()
    {
        // Arrange
        TimeProviderAccessor.Reset();

        // Act
        var result = TimeProviderAccessor.Current;

        // Assert
        result.ShouldBe(TimeProvider.System);
    }

    /// <summary>
    /// Verifies that setting <see cref="TimeProviderAccessor.Current"/> returns the set instance.
    /// </summary>
    [Fact]
    public void Current_WhenSetToFakeProvider_ReturnsFakeProvider()
    {
        // Arrange
        var fake = new FakeTimeProvider();

        // Act
        TimeProviderAccessor.Current = fake;
        var result = TimeProviderAccessor.Current;

        // Assert
        result.ShouldBe(fake);
        result.ShouldNotBe(TimeProvider.System);
    }

    /// <summary>
    /// Verifies that <see cref="TimeProviderAccessor.Reset"/> clears the current value,
    /// causing fallback to <see cref="TimeProvider.System"/>.
    /// </summary>
    [Fact]
    public void Reset_AfterSettingCustomProvider_ReturnsSystemProvider()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        TimeProviderAccessor.Current = fake;

        // Act
        TimeProviderAccessor.Reset();
        var result = TimeProviderAccessor.Current;

        // Assert
        result.ShouldBe(TimeProvider.System);
    }

    /// <summary>
    /// Verifies that <see cref="TimeProviderAccessor.SetCurrent"/> sets the current provider.
    /// </summary>
    [Fact]
    public void SetCurrent_WithFakeProvider_SetsCurrentToFakeProvider()
    {
        // Arrange
        var fake = new FakeTimeProvider();

        // Act
        TimeProviderAccessor.SetCurrent(fake);
        var result = TimeProviderAccessor.Current;

        // Assert
        result.ShouldBe(fake);
    }

    /// <summary>
    /// Verifies that <see cref="AsyncLocal{T}"/> flows correctly across async/await boundaries.
    /// </summary>
    [Fact]
    public async Task Current_WhenSetInAsyncContext_FlowsAcrossAwait()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        var captured = default(TimeProvider);

        // Act
        await Task.Run(() =>
        {
            TimeProviderAccessor.Current = fake;
            captured = TimeProviderAccessor.Current;
        });

        // Assert
        captured.ShouldBe(fake);
        TimeProviderAccessor.Current.ShouldBe(TimeProvider.System); // Outside task
    }

    /// <summary>
    /// Verifies that different async contexts have isolated <see cref="TimeProvider"/> values.
    /// </summary>
    [Fact]
    public async Task Current_WhenSetInParallelTasks_RemainIsolated()
    {
        // Arrange
        var fake1 = new FakeTimeProvider();
        var fake2 = new FakeTimeProvider();
        var result1 = default(TimeProvider);
        var result2 = default(TimeProvider);

        // Act
        var task1 = Task.Run(() =>
        {
            TimeProviderAccessor.Current = fake1;
            return TimeProviderAccessor.Current;
        });

        var task2 = Task.Run(() =>
        {
            TimeProviderAccessor.Current = fake2;
            return TimeProviderAccessor.Current;
        });

        await Task.WhenAll(task1, task2);

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        result1 = task1.Result;
        result2 = task2.Result;
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method

        // Assert
        result1.ShouldBe(fake1);
        result2.ShouldBe(fake2);
        TimeProviderAccessor.Current.ShouldBe(TimeProvider.System); // Main thread unchanged
    }

    /// <summary>
    /// Verifies that setting <c>null</c> via setter behaves like <c>Reset()</c>.
    /// </summary>
    [Fact]
    public void Current_WhenSetToNull_FallsBackToSystemProvider()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        TimeProviderAccessor.Current = fake;

        // Act
        TimeProviderAccessor.Current = null;
        var result = TimeProviderAccessor.Current;

        // Assert
        result.ShouldBe(TimeProvider.System);
    }

    /// <summary>
    /// Verifies thread isolation: different threads do not share the same value.
    /// </summary>
    [Fact]
    public void Current_WhenSetOnDifferentThreads_RemainIsolated()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        var threadResult = default(TimeProvider);

        // Act
        var thread = new System.Threading.Thread(() =>
        {
            TimeProviderAccessor.Current = fake;
            threadResult = TimeProviderAccessor.Current;
        });

        thread.Start();
        thread.Join();

        // Assert
        threadResult.ShouldBe(fake);
        TimeProviderAccessor.Current.ShouldBe(TimeProvider.System); // Main thread unchanged
    }

    /// <summary>
    /// Verifies that <see cref="TimeProviderAccessor.Current"/> works correctly
    /// when used inside a <c>using</c> block with <c>Reset()</c> in <c>Dispose</c>.
    /// </summary>
    [Fact]
    public void Current_WhenUsedWithUsingBlock_IsRestoredAfterDispose()
    {
        // Arrange
        var original = TimeProviderAccessor.Current;
        var fake = new FakeTimeProvider();

        // Act
        using (new TestTimeScope(fake))
        {
            TimeProviderAccessor.Current.ShouldBe(fake);
        }

        // Assert
        TimeProviderAccessor.Current.ShouldBe(original);
    }
}

/// <summary>
/// Helper for scoped ambient <see cref="TimeProvider"/> in tests.
/// Automatically resets on dispose.
/// </summary>
file class TestTimeScope : IDisposable
{
    private readonly TimeProvider original;

    public TestTimeScope(TimeProvider provider)
    {
        this.original = TimeProviderAccessor.Current;
        TimeProviderAccessor.Current = provider;
    }

    public void Dispose()
    {
        TimeProviderAccessor.Current = this.original;
    }
}