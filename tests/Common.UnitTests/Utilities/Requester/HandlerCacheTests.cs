// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Requester;
using Shouldly;
using Xunit;

public class HandlerCacheTests
{
    private readonly IHandlerCache handlerCache;

    public HandlerCacheTests()
    {
        this.handlerCache = new HandlerCache();
    }

    [Fact]
    public void TryAdd_NonGenericHandler_Succeeds()
    {
        // Arrange
        var requestType = typeof(MyTestRequest);
        var valueType = typeof(string);
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, valueType);
        var handlerType = typeof(MyTestRequestHandler);

        // Act
        var result = this.handlerCache.TryAdd(handlerInterface, handlerType);

        // Assert
        result.ShouldBeTrue();
        this.handlerCache.TryGetValue(handlerInterface, out var retrievedType).ShouldBeTrue();
        retrievedType.ShouldBe(handlerType);
    }

    [Fact]
    public void TryAdd_ClosedGenericHandler_Succeeds()
    {
        // Arrange
        var requestType = typeof(ProcessDataRequest<UserData>);
        var valueType = typeof(string);
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, valueType);
        var handlerType = typeof(GenericDataProcessor<UserData>);

        // Act
        var result = this.handlerCache.TryAdd(handlerInterface, handlerType);

        // Assert
        result.ShouldBeTrue();
        this.handlerCache.TryGetValue(handlerInterface, out var retrievedType).ShouldBeTrue();
        retrievedType.ShouldBe(handlerType);
    }

    [Fact]
    public void TryAdd_DuplicateHandler_ReturnsFalse()
    {
        // Arrange
        var requestType = typeof(MyTestRequest);
        var valueType = typeof(string);
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, valueType);
        var handlerType = typeof(MyTestRequestHandler);

        // Act
        this.handlerCache.TryAdd(handlerInterface, handlerType).ShouldBeTrue(); // First add succeeds
        var result = this.handlerCache.TryAdd(handlerInterface, handlerType); // Second add should fail

        // Assert
        result.ShouldBeFalse(); // Duplicate add should return false
        this.handlerCache.TryGetValue(handlerInterface, out var retrievedType).ShouldBeTrue();
        retrievedType.ShouldBe(handlerType); // Original handler type should still be present
    }

    [Fact]
    public void TryGetValue_NonExistentHandler_ReturnsFalse()
    {
        // Arrange
        var requestType = typeof(NonExistentRequest);
        var valueType = typeof(string);
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, valueType);

        // Act
        var result = this.handlerCache.TryGetValue(handlerInterface, out var handlerType);

        // Assert
        result.ShouldBeFalse();
        handlerType.ShouldBeNull();
    }

    [Fact]
    public async Task TryAdd_ConcurrentAccess_Succeeds()
    {
        // Arrange
        var requestType = typeof(MyTestRequest);
        var valueType = typeof(string);
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, valueType);
        var handlerType = typeof(MyTestRequestHandler);

        // Act
        var tasks = new List<Task<bool>>();
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => this.handlerCache.TryAdd(handlerInterface, handlerType)));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(r => r == true).ShouldBe(1); // Only one should succeed
        results.Count(r => r == false).ShouldBe(99); // The rest should fail due to duplicate key
        this.handlerCache.TryGetValue(handlerInterface, out var retrievedType).ShouldBeTrue();
        retrievedType.ShouldBe(handlerType);
    }
}

/// <summary>
/// Defines a non-existent request type for testing
/// </summary>
public class NonExistentRequest : RequestBase<string>;
