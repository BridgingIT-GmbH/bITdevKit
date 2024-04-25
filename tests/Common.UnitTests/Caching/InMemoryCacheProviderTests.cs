// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Caching;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

public class InMemoryCacheProviderTests
{
    private readonly string key = $"testKey{DateTime.UtcNow.Ticks}";
    private readonly string value = $"testValue{DateTime.UtcNow.Ticks}";
    private readonly InMemoryCacheProvider sut;
    private readonly IMemoryCache memoryCache;

    public InMemoryCacheProviderTests(ITestOutputHelper output)
    {
        this.memoryCache = new MemoryCache(new MemoryCacheOptions()); // Substitute.For<IMemoryCache>();
        this.sut = new InMemoryCacheProvider(XunitLoggerFactory.Create(output), this.memoryCache);
        this.sut.Set(this.key, this.value);
    }

    [Fact]
    public void Get_WithValidKey_ShouldReturnValue()
    {
        // Arrange
        // Act
        var result = this.sut.Get<string>(this.key);

        // Assert
        result.ShouldBe(this.value);
    }

    [Fact]
    public async Task GetAsync_WithValidKey_ShouldReturnValue()
    {
        // Arrange
        // Act
        var result = await this.sut.GetAsync<string>(this.key);

        // Assert
        result.ShouldBe(this.value);
    }

    [Fact]
    public void Get_WithInvalidKey_ShouldNotReturnValue()
    {
        // Arrange
        // Act
        var result = this.sut.Get<string>(this.key + "INVALID");

        // Assert
        result.ShouldBe(null);
    }

    [Fact]
    public async Task GetAsync_WithInvalidKey_ShouldNotReturnValue()
    {
        // Arrange
        // Act
        var result = await this.sut.GetAsync<string>(this.key + "INVALID");

        // Assert
        result.ShouldBe(null);
    }

    [Fact]
    public void TryGet_WithValidKey_ShouldReturnTrue()
    {
        // Arrange
        // Act
        var result = this.sut.TryGet<string>(this.key, out var value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe(this.value);
    }

    [Fact]
    public async Task TryGetAsync_WithValidKey_ShouldReturnTrue()
    {
        // Arrange
        // Act
        var result = await this.sut.TryGetAsync<string>(this.key, out var value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe(this.value);
    }

    [Fact]
    public void TryGet_WithInvalidKey_ShouldReturnFalse()
    {
        // Arrange
        // Act
        var result = this.sut.TryGet<string>(this.key + "INVALID", out var value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe(null);
    }

    [Fact]
    public async Task TryGetAsync_WithInvalidKey_ShouldReturnFalse()
    {
        // Arrange
        // Act
        var result = await this.sut.TryGetAsync<string>(this.key + "INVALID", out var value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe(null);
    }

    [Fact]
    public void TryGetKeys_WithExistingEntries_ShouldReturnKeys()
    {
        // Arrange
        // Act
        var result = this.sut.GetKeys();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(this.key);
    }

    [Fact]
    public async Task TryGetKeysAsync_WithExistingEntries_ShouldReturnKeys()
    {
        // Arrange
        // Act
        var result = await this.sut.GetKeysAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(this.key);
    }

    [Fact]
    public void Remove_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        // Act
        this.sut.Remove(this.key);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsync_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        // Act
        await this.sut.RemoveAsync(this.key);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    [Fact]
    public void RemoveStartsWith_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        const string key = "test";

        // Act
        this.sut.RemoveStartsWith(key);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveStartsWithAsync_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        const string key = "test";

        // Act
        await this.sut.RemoveStartsWithAsync(key);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    [Fact]
    public void Set_WithValidDataNoExpiration_ShouldReturnValue()
    {
        // Arrange
        // Act
        this.sut.Set(this.key, this.value);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeTrue();
    }

    [Fact]
    public void Set_WithValidData_ShouldReturnValue()
    {
        // Arrange
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        this.sut.Set(this.key, this.value, slidingExpiration, absoluteExpiration);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task SetAsync_WithValidData_ShouldReturnValue()
    {
        // Arrange
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await this.sut.SetAsync(this.key, this.value, slidingExpiration, absoluteExpiration);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeTrue();
    }

    [Fact]
    public void Set_WithValidButExpiredData_ShouldNotReturnValue()
    {
        // Arrange
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        this.sut.Set(this.key, this.value, slidingExpiration, absoluteExpiration);

        // Assert
        this.sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }
}