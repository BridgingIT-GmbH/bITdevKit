// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="RequesterBuilder"/> options configuration methods.
/// </summary>
public class RequesterBuilderOptionsTests
{
    /// <summary>
    /// Tests that WithRetryOptions with parameters correctly configures RetryOptions.
    /// </summary>
    [Fact]
    public void WithRetryOptions_WithParameters_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithRetryOptions(3, 100);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<RetryOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultCount.ShouldBe(3);
        options.Value.DefaultDelay.ShouldBe(100);
    }

    /// <summary>
    /// Tests that WithRetryOptions with action correctly configures RetryOptions.
    /// </summary>
    [Fact]
    public void WithRetryOptions_WithAction_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithRetryOptions(options =>
            {
                options.DefaultCount = 5;
                options.DefaultDelay = 200;
            });
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<RetryOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultCount.ShouldBe(5);
        options.Value.DefaultDelay.ShouldBe(200);
    }

    /// <summary>
    /// Tests that WithTimeoutOptions with parameter correctly configures TimeoutOptions.
    /// </summary>
    [Fact]
    public void WithTimeoutOptions_WithParameter_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithTimeoutOptions(300);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<TimeoutOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultDuration.ShouldBe(300);
    }

    /// <summary>
    /// Tests that WithTimeoutOptions with action correctly configures TimeoutOptions.
    /// </summary>
    [Fact]
    public void WithTimeoutOptions_WithAction_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithTimeoutOptions(options => options.DefaultDuration = 500);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<TimeoutOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultDuration.ShouldBe(500);
    }

    /// <summary>
    /// Tests that WithCircuitBreakerOptions with parameters correctly configures CircuitBreakerOptions.
    /// </summary>
    [Fact]
    public void WithCircuitBreakerOptions_WithParameters_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithCircuitBreakerOptions(5, 60, 1000, true);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<CircuitBreakerOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultAttempts.ShouldBe(5);
        options.Value.DefaultBreakDurationSeconds.ShouldBe(60);
        options.Value.DefaultBackoffMilliseconds.ShouldBe(1000);
        options.Value.DefaultBackoffExponential.ShouldBe(true);
    }

    /// <summary>
    /// Tests that WithCircuitBreakerOptions with action correctly configures CircuitBreakerOptions.
    /// </summary>
    [Fact]
    public void WithCircuitBreakerOptions_WithAction_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithCircuitBreakerOptions(options =>
            {
                options.DefaultAttempts = 3;
                options.DefaultBreakDurationSeconds = 30;
                options.DefaultBackoffMilliseconds = 500;
                options.DefaultBackoffExponential = false;
            });
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<CircuitBreakerOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultAttempts.ShouldBe(3);
        options.Value.DefaultBreakDurationSeconds.ShouldBe(30);
        options.Value.DefaultBackoffMilliseconds.ShouldBe(500);
        options.Value.DefaultBackoffExponential.ShouldBe(false);
    }

    /// <summary>
    /// Tests that WithChaosOptions with parameters correctly configures ChaosOptions.
    /// </summary>
    [Fact]
    public void WithChaosOptions_WithParameters_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithChaosOptions(0.5, true);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<ChaosOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultInjectionRate.ShouldBe(0.5);
        options.Value.DefaultEnabled.ShouldBe(true);
    }

    /// <summary>
    /// Tests that WithChaosOptions with action correctly configures ChaosOptions.
    /// </summary>
    [Fact]
    public void WithChaosOptions_WithAction_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithChaosOptions(options =>
            {
                options.DefaultInjectionRate = 0.2;
                options.DefaultEnabled = false;
            });
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<ChaosOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultInjectionRate.ShouldBe(0.2);
        options.Value.DefaultEnabled.ShouldBe(false);
    }

    /// <summary>
    /// Tests that WithBehaviorOptions correctly configures generic options.
    /// </summary>
    [Fact]
    public void WithBehaviorOptions_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithBehaviorOptions<RetryOptions>(options =>
            {
                options.DefaultCount = 10;
                options.DefaultDelay = 50;
            });
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<RetryOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultCount.ShouldBe(10);
        options.Value.DefaultDelay.ShouldBe(50);
    }

    /// <summary>
    /// Tests that multiple options can be configured in a fluent chain.
    /// </summary>
    [Fact]
    public void FluentChain_ConfiguresMultipleOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithRetryOptions(3, 100)
            .WithTimeoutOptions(300)
            .WithChaosOptions(0.1, false)
            .WithCircuitBreakerOptions(5, 60, 1000);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var retryOptions = serviceProvider.GetService<IOptions<RetryOptions>>();
        var timeoutOptions = serviceProvider.GetService<IOptions<TimeoutOptions>>();
        var chaosOptions = serviceProvider.GetService<IOptions<ChaosOptions>>();
        var circuitBreakerOptions = serviceProvider.GetService<IOptions<CircuitBreakerOptions>>();

        // Assert
        retryOptions.Value.DefaultCount.ShouldBe(3);
        retryOptions.Value.DefaultDelay.ShouldBe(100);

        timeoutOptions.Value.DefaultDuration.ShouldBe(300);

        chaosOptions.Value.DefaultInjectionRate.ShouldBe(0.1);
        chaosOptions.Value.DefaultEnabled.ShouldBe(false);

        circuitBreakerOptions.Value.DefaultAttempts.ShouldBe(5);
        circuitBreakerOptions.Value.DefaultBreakDurationSeconds.ShouldBe(60);
        circuitBreakerOptions.Value.DefaultBackoffMilliseconds.ShouldBe(1000);
    }

    /// <summary>
    /// Tests that options can be configured alongside handlers and behaviors.
    /// </summary>
    [Fact]
    public void WithOptions_WorksWithHandlersAndBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(RetryPipelineBehavior<,>))
            .WithRetryOptions(3, 100)
            .WithBehavior(typeof(TimeoutPipelineBehavior<,>))
            .WithTimeoutOptions(500);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var requester = serviceProvider.GetService<IRequester>();
        var retryOptions = serviceProvider.GetService<IOptions<RetryOptions>>();
        var timeoutOptions = serviceProvider.GetService<IOptions<TimeoutOptions>>();

        // Assert
        requester.ShouldNotBeNull();
        retryOptions.Value.DefaultCount.ShouldBe(3);
        retryOptions.Value.DefaultDelay.ShouldBe(100);
        timeoutOptions.Value.DefaultDuration.ShouldBe(500);
    }

    /// <summary>
    /// Tests that RequesterBuilder returns itself for fluent chaining.
    /// </summary>
    [Fact]
    public void OptionsMethodsReturnBuilder_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var builder1 = services.AddRequester();
        var builder2 = builder1.WithRetryOptions(3, 100);
        var builder3 = builder2.WithTimeoutOptions(300);

        // Assert
        builder1.ShouldBeOfType<RequesterBuilder>();
        builder2.ShouldBeOfType<RequesterBuilder>();
        builder3.ShouldBeOfType<RequesterBuilder>();
        builder2.ShouldBeSameAs(builder1);
        builder3.ShouldBeSameAs(builder1);
    }

    /// <summary>
    /// Tests that options configured later override earlier configurations.
    /// </summary>
    [Fact]
    public void MultipleConfigurationsOfSameOption_LastOneWins()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithRetryOptions(3, 100)
            .WithRetryOptions(5, 200); // This should override
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<RetryOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        // Note: With IOptions<T>.Configure, both configurations are applied in order
        // The last configuration sets the values
        options.Value.DefaultCount.ShouldBe(5);
        options.Value.DefaultDelay.ShouldBe(200);
    }

    /// <summary>
    /// Tests that Services property is accessible for extension methods.
    /// </summary>
    [Fact]
    public void ServicesProperty_IsAccessible()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddRequester();

        // Act
        var serviceCollection = builder.Services;

        // Assert
        serviceCollection.ShouldNotBeNull();
        serviceCollection.ShouldBeSameAs(services);
    }
}
