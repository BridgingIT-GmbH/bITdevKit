// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the EntityFramework-specific <see cref="RequesterBuilder"/> options configuration methods.
/// </summary>
public class RequesterBuilderEntityFrameworkOptionsTests
{
    /// <summary>
    /// Tests that WithDatabaseTransactionOptions with parameter correctly configures DatabaseTransactionOptions.
    /// </summary>
    [Fact]
    public void WithDatabaseTransactionOptions_WithParameter_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithDatabaseTransactionOptions("Core");
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultContextName.ShouldBe("Core");
    }

    /// <summary>
    /// Tests that WithDatabaseTransactionOptions with action correctly configures DatabaseTransactionOptions.
    /// </summary>
    [Fact]
    public void WithDatabaseTransactionOptions_WithAction_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithDatabaseTransactionOptions(options => options.DefaultContextName = "CoreDbContext");
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultContextName.ShouldBe("CoreDbContext");
    }

    /// <summary>
    /// Tests that WithDatabaseTransactionOptions can be chained with other options.
    /// </summary>
    [Fact]
    public void WithDatabaseTransactionOptions_CanBeChainedWithOtherOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithDatabaseTransactionOptions("Core")
            .WithRetryOptions(3, 100)
            .WithTimeoutOptions(500);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var databaseOptions = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();
        var retryOptions = serviceProvider.GetService<IOptions<RetryOptions>>();
        var timeoutOptions = serviceProvider.GetService<IOptions<TimeoutOptions>>();

        // Assert
        databaseOptions.Value.DefaultContextName.ShouldBe("Core");
        retryOptions.Value.DefaultCount.ShouldBe(3);
        retryOptions.Value.DefaultDelay.ShouldBe(100);
        timeoutOptions.Value.DefaultDuration.ShouldBe(500);
    }

    /// <summary>
    /// Tests that WithDatabaseTransactionOptions returns RequesterBuilder for fluent chaining.
    /// </summary>
    [Fact]
    public void WithDatabaseTransactionOptions_ReturnsBuilder_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var builder1 = services.AddRequester();
        var builder2 = builder1.WithDatabaseTransactionOptions("Core");
        var builder3 = builder2.WithDatabaseTransactionOptions(options => options.DefaultContextName = "Tenant");

        // Assert
        builder1.ShouldBeOfType<RequesterBuilder>();
        builder2.ShouldBeOfType<RequesterBuilder>();
        builder3.ShouldBeOfType<RequesterBuilder>();
        builder2.ShouldBeSameAs(builder1);
        builder3.ShouldBeSameAs(builder1);
    }

    /// <summary>
    /// Tests that multiple configurations of DatabaseTransactionOptions, the last one wins.
    /// </summary>
    [Fact]
    public void MultipleDatabaseTransactionOptionsConfigurations_LastOneWins()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithDatabaseTransactionOptions("Core")
            .WithDatabaseTransactionOptions("Tenant"); // This should override
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultContextName.ShouldBe("Tenant");
    }

    /// <summary>
    /// Tests that WithDatabaseTransactionOptions works with handlers and behaviors.
    /// </summary>
    [Fact]
    public void WithDatabaseTransactionOptions_WorksWithHandlersAndBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddRequester()
            .AddHandlers()
            .WithDatabaseTransactionOptions("Core");
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var requester = serviceProvider.GetService<IRequester>();
        var options = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();

        // Assert
        requester.ShouldNotBeNull();
        options.Value.DefaultContextName.ShouldBe("Core");
    }

    /// <summary>
    /// Tests that all options (common and EntityFramework) can be configured together.
    /// </summary>
    [Fact]
    public void AllOptions_CanBeConfiguredTogether()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithRetryOptions(3, 100)
            .WithTimeoutOptions(300)
            .WithCircuitBreakerOptions(5, 60, 1000)
            .WithChaosOptions(0.1, false)
            .WithDatabaseTransactionOptions("Core");
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var retryOptions = serviceProvider.GetService<IOptions<RetryOptions>>();
        var timeoutOptions = serviceProvider.GetService<IOptions<TimeoutOptions>>();
        var circuitBreakerOptions = serviceProvider.GetService<IOptions<CircuitBreakerOptions>>();
        var chaosOptions = serviceProvider.GetService<IOptions<ChaosOptions>>();
        var databaseOptions = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();

        // Assert
        retryOptions.Value.DefaultCount.ShouldBe(3);
        retryOptions.Value.DefaultDelay.ShouldBe(100);

        timeoutOptions.Value.DefaultDuration.ShouldBe(300);

        circuitBreakerOptions.Value.DefaultAttempts.ShouldBe(5);
        circuitBreakerOptions.Value.DefaultBreakDurationSeconds.ShouldBe(60);
        circuitBreakerOptions.Value.DefaultBackoffMilliseconds.ShouldBe(1000);

        chaosOptions.Value.DefaultInjectionRate.ShouldBe(0.1);
        chaosOptions.Value.DefaultEnabled.ShouldBe(false);

        databaseOptions.Value.DefaultContextName.ShouldBe("Core");
    }

    /// <summary>
    /// Tests that WithDatabaseTransactionOptions can handle null or empty context names.
    /// </summary>
    [Fact]
    public void WithDatabaseTransactionOptions_CanHandleNullOrEmptyContextName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .WithDatabaseTransactionOptions(string.Empty);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetService<IOptions<DatabaseTransactionOptions>>();

        // Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        options.Value.DefaultContextName.ShouldBe(string.Empty);
    }
}
