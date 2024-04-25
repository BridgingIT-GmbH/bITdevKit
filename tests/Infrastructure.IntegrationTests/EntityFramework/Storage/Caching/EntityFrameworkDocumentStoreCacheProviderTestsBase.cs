// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;
using BridgingIT.DevKit.Application.Storage;

public abstract class EntityFrameworkDocumentStoreCacheProviderTestsBase
{
    protected string key = $"testKey{DateTime.UtcNow.Ticks}";
    protected string value = $"testValue{DateTime.UtcNow.Ticks}";

    public virtual async Task GetAsync_WithInvalidKey_ShouldNotReturnValue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = await sut.GetAsync<string>(this.key + "INVALID");

        // Assert
        result.ShouldBe(null);
    }

    public virtual async Task GetAsync_WithValidKey_ShouldReturnValue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = await sut.GetAsync<string>(this.key);

        // Assert
        result.ShouldBe(this.value);
    }

    public virtual void Get_WithInvalidKey_ShouldNotReturnValue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = sut.Get<string>(this.key + "INVALID");

        // Assert
        result.ShouldBe(null);
    }

    public virtual void Get_WithValidKey_ShouldReturnValue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = sut.Get<string>(this.key);

        // Assert
        result.ShouldBe(this.value);
    }

    public virtual async Task RemoveAsync_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        await sut.RemoveAsync(this.key);

        // Assert
        sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    public virtual async Task RemoveStartsWithAsync_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        const string key = "test";
        var sut = this.GetProvider();

        // Act
        await sut.RemoveStartsWithAsync(key);

        // Assert
        sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    public virtual void RemoveStartsWith_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        const string key = "test";
        var sut = this.GetProvider();

        // Act
        sut.RemoveStartsWith(key);

        // Assert
        sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    public virtual void Remove_WithValidKey_ShouldRemoveValue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        sut.Remove(this.key);

        // Assert
        sut.TryGet<string>(this.key, out _).ShouldBeFalse();
    }

    public virtual async Task SetAsync_WithValidData_ShouldReturnValue()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
        var sut = this.GetProvider();

        // Act
        await sut.SetAsync(key, value, slidingExpiration, absoluteExpiration);

        // Assert
        sut.TryGet<string>(key, out _).ShouldBeTrue();
    }

    public virtual void Set_WithValidButExpiredData_ShouldNotReturnValue()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1);
        var sut = this.GetProvider();

        // Act
        sut.Set(key, value, slidingExpiration, absoluteExpiration);

        // Assert
        sut.TryGet<string>(key, out _).ShouldBeFalse();
    }

    public virtual void Set_WithValidDataNoExpiration_ShouldReturnValue()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var sut = this.GetProvider();

        // Act
        sut.Set(key, value);

        // Assert
        sut.TryGet<string>(key, out _).ShouldBeTrue();
    }

    public virtual void Set_WithValidData_ShouldReturnValue()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var key = "testKey" + ticks;
        var value = "testValue" + ticks;
        var slidingExpiration = TimeSpan.FromMinutes(30);
        var absoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
        var sut = this.GetProvider();

        // Act
        sut.Set(key, value, slidingExpiration, absoluteExpiration);

        // Assert
        sut.TryGet<string>(key, out _).ShouldBeTrue();
    }

    public virtual async Task TryGetAsync_WithInvalidKey_ShouldReturnFalse()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = await sut.TryGetAsync<string>(this.key + "INVALID", out var value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe(null);
    }

    public virtual async Task TryGetAsync_WithValidKey_ShouldReturnTrue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = await sut.TryGetAsync<string>(this.key, out var value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe(this.value);
    }

    public virtual async Task TryGetKeysAsync_WithExistingEntries_ShouldReturnKeys()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = await sut.GetKeysAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(this.key);
    }

    public virtual void TryGetKeys_WithExistingEntries_ShouldReturnKeys()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = sut.GetKeys();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(this.key);
    }

    public virtual void TryGet_WithInvalidKey_ShouldReturnFalse()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = sut.TryGet<string>(this.key + "INVALID", out var value);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe(null);
    }

    public virtual void TryGet_WithValidKey_ShouldReturnTrue()
    {
        // Arrange
        var sut = this.GetProvider();

        // Act
        var result = sut.TryGet<string>(this.key, out var value);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe(this.value);
    }

    protected virtual DocumentStoreCacheProvider GetProvider()
    {
        return null;
    }
}