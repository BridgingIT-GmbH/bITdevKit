// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions.AddTimeProvider"/> overloads,
/// ensuring correct DI registration and ambient synchronization.
/// </summary>
public class TimeProviderRegistrationTests : IDisposable
{
    private readonly TimeProvider originalAccessor;

    public TimeProviderRegistrationTests()
    {
        this.originalAccessor = TimeProviderAccessor.Current;
    }

    public void Dispose()
    {
        TimeProviderAccessor.Current = this.originalAccessor;
    }

    /// <summary>
    /// Verifies that <c>AddTimeProvider()</c> registers <see cref="TimeProvider.System"/>
    /// and sets it as ambient current.
    /// </summary>
    [Fact]
    public void AddTimeProvider_NoArgs_RegistersSystemProviderAndSetsAmbient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTimeProvider();

        // Assert
        var sp = services.BuildServiceProvider();
        var diProvider = sp.GetRequiredService<TimeProvider>();
        var ambientProvider = TimeProviderAccessor.Current;

        diProvider.ShouldBe(TimeProvider.System);
        ambientProvider.ShouldBe(TimeProvider.System);
    }

    /// <summary>
    /// Verifies that <c>AddTimeProvider(TimeProvider)</c> registers the instance
    /// and synchronizes ambient context.
    /// </summary>
    [Fact]
    public void AddTimeProvider_WithCustomProvider_RegistersAndSetsAmbient()
    {
        // Arrange
        var services = new ServiceCollection();
        var fake = new FakeTimeProvider();

        // Act
        services.AddTimeProvider(fake);

        // Assert
        var sp = services.BuildServiceProvider();
        var diProvider = sp.GetRequiredService<TimeProvider>();
        var ambientProvider = TimeProviderAccessor.Current;

        diProvider.ShouldBe(fake);
        ambientProvider.ShouldBe(fake);
    }

    /// <summary>
    /// Verifies that <c>AddTimeProvider(DateTimeOffset)</c> creates a <see cref="FakeTimeProvider"/>
    /// with the correct start time.
    /// </summary>
    [Fact]
    public void AddTimeProvider_WithDateTimeOffset_RegistersFakeProviderWithStartTime()
    {
        // Arrange
        var services = new ServiceCollection();
        var start = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        services.AddTimeProvider(start);

        // Assert
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<TimeProvider>().ShouldBeOfType<FakeTimeProvider>();
        var ambientProvider = TimeProviderAccessor.Current.ShouldBeOfType<FakeTimeProvider>();

        provider.GetUtcNow().ShouldBe(start);
        ambientProvider.GetUtcNow().ShouldBe(start);
    }

    /// <summary>
    /// Verifies that <c>AddTimeProvider(DateTime)</c> treats input as UTC and converts correctly.
    /// </summary>
    [Fact]
    public void AddTimeProvider_WithDateTime_ConvertsToUtcDateTimeOffset()
    {
        // Arrange
        var services = new ServiceCollection();
        var utcNow = DateTime.SpecifyKind(new DateTime(2025, 1, 1, 12, 0, 0), DateTimeKind.Utc);

        // Act
        services.AddTimeProvider(utcNow);

        // Assert
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<TimeProvider>().ShouldBeOfType<FakeTimeProvider>();

        provider.GetUtcNow().UtcDateTime.ShouldBe(utcNow);
        TimeProviderAccessor.Current.GetUtcNow().UtcDateTime.ShouldBe(utcNow);
    }

    /// <summary>
    /// Verifies that <c>AddTimeProvider(Func&lt;IServiceProvider, TimeProvider&gt;)</c>
    /// uses the factory and syncs ambient.
    /// </summary>
    [Fact]
    public void AddTimeProvider_WithFactory_InvokesFactoryAndSetsAmbient()
    {
        // Arrange
        var services = new ServiceCollection();
        var expected = new FakeTimeProvider();

        // Act
        services.AddTimeProvider(_ => expected);

        // Assert
        var sp = services.BuildServiceProvider();
        var diProvider = sp.GetRequiredService<TimeProvider>();
        var ambientProvider = TimeProviderAccessor.Current;

        diProvider.ShouldBe(expected);
        ambientProvider.ShouldBe(expected);
    }

    /// <summary>
    /// Verifies that multiple calls replace the previous registration (Replace behavior).
    /// </summary>
    [Fact]
    public void AddTimeProvider_MultipleCalls_ReplacesPreviousRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var first = new FakeTimeProvider();
        var second = new FakeTimeProvider();

        // Act
        services.AddTimeProvider(first);
        services.AddTimeProvider(second);

        // Assert
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<TimeProvider>();

        provider.ShouldBe(second);
        TimeProviderAccessor.Current.ShouldBe(second);
    }

    /// <summary>
    /// Verifies that ambient context is set **immediately** during registration,
    /// even before <c>BuildServiceProvider()</c>.
    /// </summary>
    [Fact]
    public void AddTimeProvider_SetsAmbientImmediately_BeforeBuild()
    {
        // Arrange
        var services = new ServiceCollection();
        var fake = new FakeTimeProvider();

        // Act
        services.AddTimeProvider(fake);

        // Assert (no Build yet!)
        TimeProviderAccessor.Current.ShouldBe(fake);
    }

    /// <summary>
    /// Verifies that ambient context flows across async boundaries after registration.
    /// </summary>
    [Fact]
    public async Task AddTimeProvider_AmbientFlowsAcrossAsync_AfterRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var fake = new FakeTimeProvider();
        services.AddTimeProvider(fake);

        var captured = default(TimeProvider);

        // Act
        await Task.Run(() =>
        {
            captured = TimeProviderAccessor.Current;
        });

        // Assert
        captured.ShouldBe(fake);
    }

    /// <summary>
    /// Verifies that <c>null</c> provider throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void AddTimeProvider_WithNullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddTimeProvider((TimeProvider)null));
    }

    /// <summary>
    /// Verifies that <c>null</c> factory throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void AddTimeProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddTimeProvider((Func<IServiceProvider, TimeProvider>)null));
    }
}