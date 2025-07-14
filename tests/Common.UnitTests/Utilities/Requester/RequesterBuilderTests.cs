// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="RequesterBuilder"/> class.
/// </summary>
public class RequesterBuilderTests
{
    /// <summary>
    /// Tests that AddHandlers successfully registers handlers and sets up DI services.
    /// </summary>
    [Fact]
    public void AddHandlers_RegistersHandlersAndServices_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);

        // Act
        builder.AddHandlers(["^System\\..*"]);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var requester = serviceProvider.GetService<IRequester>();
        requester.ShouldNotBeNull();

        var handlerProvider = serviceProvider.GetService<IRequestHandlerProvider>();
        handlerProvider.ShouldNotBeNull();

        var behaviorsProvider = serviceProvider.GetService<IRequestBehaviorsProvider>();
        behaviorsProvider.ShouldNotBeNull();

        var handlerCache = serviceProvider.GetService<IHandlerCache>();
        handlerCache.ShouldNotBeNull();
        handlerCache.ShouldContainKey(typeof(IRequestHandler<MyTestRequest, string>));
        handlerCache[typeof(IRequestHandler<MyTestRequest, string>)].ShouldBe(typeof(MyTestRequestHandler));
    }

    /// <summary>
    /// Tests that WithBehavior adds a single behavior to the pipeline.
    /// </summary>
    [Fact]
    public void WithBehavior_SingleBehavior_AddsToPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);

        // Act
        builder.WithBehavior(typeof(TestBehavior<,>));
        builder.AddHandlers(["^System\\..*"]);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var behaviorsProvider = serviceProvider.GetService<IRequestBehaviorsProvider>();
        var behaviors = behaviorsProvider.GetBehaviors<MyTestRequest, string>(serviceProvider);
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(1);
        behaviors[0].ShouldBeOfType(typeof(TestBehavior<MyTestRequest, IResult<string>>));
    }

    /// <summary>
    /// Tests that multiple WithBehavior calls add behaviors in the correct order.
    /// </summary>
    [Fact]
    public void WithBehavior_MultipleBehaviors_AddsInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);

        // Act
        builder.WithBehavior(typeof(TestBehavior<,>))
               .WithBehavior(typeof(AnotherTestBehavior<,>));
        builder.AddHandlers(["^System\\..*"]);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var behaviorsProvider = serviceProvider.GetService<IRequestBehaviorsProvider>();
        var behaviors = behaviorsProvider.GetBehaviors<MyTestRequest, string>(serviceProvider);
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(2);
        behaviors[0].ShouldBeOfType(typeof(TestBehavior<MyTestRequest, IResult<string>>));
        behaviors[1].ShouldBeOfType(typeof(AnotherTestBehavior<MyTestRequest, IResult<string>>));
    }

    /// <summary>
    /// Tests that constructing the builder with a null ServiceCollection throws an exception.
    /// </summary>
    [Fact]
    public void Constructor_NullServiceCollection_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentNullException>(() => new RequesterBuilder(null));

        // Assert
        exception.ParamName.ShouldBe("services");
    }

    /// <summary>
    /// Tests that AddHandlers handles the case where no handlers are found in assemblies.
    /// </summary>
    [Fact]
    public void AddHandlers_NoHandlersFound_RegistersProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);

        // Act
        builder.AddHandlers(["*"]); // blacklist all assemblies
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var requester = serviceProvider.GetService<IRequester>();
        requester.ShouldNotBeNull();

        var handlerProvider = serviceProvider.GetService<IRequestHandlerProvider>();
        handlerProvider.ShouldNotBeNull();

        var behaviorsProvider = serviceProvider.GetService<IRequestBehaviorsProvider>();
        behaviorsProvider.ShouldNotBeNull();

        var handlerCache = serviceProvider.GetService<IHandlerCache>();
        handlerCache.ShouldNotBeNull();
        handlerCache.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that AddHandlers skips assemblies matching the blacklist patterns.
    /// </summary>
    [Fact]
    public void AddHandlers_WithBlacklist_SkipsMatchingAssemblies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);

        // Act
        builder.AddHandlers(["*"]); // Blacklist all assemblies
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handlerCache = serviceProvider.GetService<IHandlerCache>();
        handlerCache.ShouldNotBeNull();
        handlerCache.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that AddHandlers correctly registers handlers despite potential reflection errors.
    /// </summary>
    [Fact]
    public void AddHandlers_ReflectionErrors_HandledGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);

        // Act
        builder.AddHandlers(["^System\\..*"]);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handlerCache = serviceProvider.GetService<IHandlerCache>();
        handlerCache.ShouldNotBeNull();
        handlerCache.ShouldContainKey(typeof(IRequestHandler<MyTestRequest, string>));
        handlerCache[typeof(IRequestHandler<MyTestRequest, string>)].ShouldBe(typeof(MyTestRequestHandler));
    }
}