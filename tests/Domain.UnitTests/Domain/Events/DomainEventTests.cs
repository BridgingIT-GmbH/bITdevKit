// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

[UnitTest("Domain")]
public class DomainEventTests
{
    [Fact]
    public void Constructor_WhenCalled_SetsEventId()
    {
        // Arrange & Act
        var sut = new StubDomainEvent();

        // Assert
        sut.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsTimestamp()
    {
        // Arrange & Act
        var sut = new StubDomainEvent();

        // Assert
        sut.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Equals_WhenSameEventId_ReturnsTrue()
    {
        // Arrange
        var sut = new StubDomainEvent();
        var other = new StubDomainEvent();
        ReflectionHelper.SetProperty(other, nameof(DomainEventBase.EventId), sut.EventId);

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentEventId_ReturnsFalse()
    {
        // Arrange
        var sut = new StubDomainEvent();
        var other = new StubDomainEvent();

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenNull_ReturnsFalse()
    {
        // Arrange
        var sut = new StubDomainEvent();

        // Act
        var result = sut.Equals(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WhenCalledMultipleTimes_ReturnsSameValue()
    {
        // Arrange
        var sut = new StubDomainEvent();

        // Act
        var hashCode1 = sut.GetHashCode();
        var hashCode2 = sut.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_ForDifferentEvents_ReturnsDifferentValues()
    {
        // Arrange
        var sut1 = new StubDomainEvent();
        var sut2 = new StubDomainEvent();

        // Act
        var hashCode1 = sut1.GetHashCode();
        var hashCode2 = sut2.GetHashCode();

        // Assert
        hashCode1.ShouldNotBe(hashCode2);
    }

    private class StubDomainEvent : DomainEventBase;
}