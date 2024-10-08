// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Caching;

using Microsoft.Extensions.Caching.Memory;

[UnitTest("Common")]
public class MemoryCacheExtensionsTests
{
    [Fact]
    public void GetKeys_All_ReturnsKeys()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.GetOrCreate(1, e => "one");
        cache.GetOrCreate(2, e => "two");
        cache.GetOrCreate("set_one", e => "one");
        cache.GetOrCreate("set_two", e => "two");
        cache.GetOrCreate("set_three", e => "three");

        // Act
        var keys = cache.GetKeys();

        // Assert
        keys.ShouldNotBeNull();
        keys.Count.ShouldBe(5);
    }

    [Fact]
    public void GetKeysInt_All_ReturnsIntKeys()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.GetOrCreate(1, e => "one");
        cache.GetOrCreate(2, e => "two");
        cache.GetOrCreate("set_one", e => "one");
        cache.GetOrCreate("set_two", e => "two");
        cache.GetOrCreate("set_three", e => "three");

        // Act
        var keys = cache.GetKeys<int>();

        // Assert
        keys.ShouldNotBeNull();
        keys.Count().ShouldBe(2);
    }

    [Fact]
    public void GetKeysString_All_ReturnsStringKeys()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.GetOrCreate(1, e => "one");
        cache.GetOrCreate(2, e => "two");
        cache.GetOrCreate("set_one", e => "one");
        cache.GetOrCreate("set_two", e => "two");
        cache.GetOrCreate("set_three", e => "three");

        // Act
        var keys = cache.GetKeys<string>();

        // Assert
        keys.ShouldNotBeNull();
        keys.Count().ShouldBe(3);
    }

    [Fact]
    public void RemoveStartsWith_String_RemovesMatchingEntries()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.GetOrCreate(1, e => "one");
        cache.GetOrCreate(2, e => "two");
        cache.GetOrCreate("set_one", e => "one");
        cache.GetOrCreate("set_two", e => "two");
        cache.GetOrCreate("set_three", e => "three");

        // Act
        cache.RemoveStartsWith("set_");
        var keys = cache.GetKeys();

        // Assert
        keys.ShouldNotBeNull();
        keys.Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveContains_String_RemovesMatchingEntries()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.GetOrCreate(1, e => "one");
        cache.GetOrCreate(2, e => "two");
        cache.GetOrCreate("set_one", e => "one");
        cache.GetOrCreate("set_two", e => "two");
        cache.GetOrCreate("set_three", e => "three");

        // Act
        cache.RemoveContains("_");
        var keys = cache.GetKeys();

        // Assert
        keys.ShouldNotBeNull();
        keys.Count.ShouldBe(2);
    }
}