// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain;

using System.Text.Json;

[UnitTest("Domain")]
public class DomainEventTests
{
    private readonly JsonSerializerOptions options;

    public DomainEventTests()
    {
        this.options = DefaultJsonSerializerOptions.Create();
    }

    [Fact]
    public void Constructor_WhenCalled_SetsEventId()
    {
        // Arrange & Act
        var sut = new StubDomainEvent(Guid.Empty);

        // Assert
        sut.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsTimestamp()
    {
        // Arrange & Act
        var sut = new StubDomainEvent(Guid.Empty);

        // Assert
        sut.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Equals_WhenSameEventId_ReturnsTrue()
    {
        // Arrange
        var sut = new StubDomainEvent(Guid.Empty);
        var other = new StubDomainEvent(Guid.Empty);
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
        var sut = new StubDomainEvent(Guid.Empty);
        var other = new StubDomainEvent(Guid.Empty);

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenNull_ReturnsFalse()
    {
        // Arrange
        var sut = new StubDomainEvent(Guid.Empty);

        // Act
        var result = sut.Equals(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WhenCalledMultipleTimes_ReturnsSameValue()
    {
        // Arrange
        var sut = new StubDomainEvent(Guid.Empty);

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
        var sut1 = new StubDomainEvent(Guid.Empty);
        var sut2 = new StubDomainEvent(Guid.Empty);

        // Act
        var hashCode1 = sut1.GetHashCode();
        var hashCode2 = sut2.GetHashCode();

        // Assert
        hashCode1.ShouldNotBe(hashCode2);
    }

    [Fact]
    public void SerializeAndDeserialize_StubDomainEvent_PreservesProperties()
    {
        // Arrange
        var originalValue = Guid.NewGuid();
        var originalEvent = new StubDomainEvent(originalValue);
        var json = JsonSerializer.Serialize(originalEvent, this.options);

        // Act
        var deserializedEvent = JsonSerializer.Deserialize<StubDomainEvent>(json, this.options);

        // Assert
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.Value.ShouldBe(originalValue);
        deserializedEvent.EventId.ShouldNotBe(Guid.Empty);
        deserializedEvent.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        // Properties dictionary should be empty by default
        deserializedEvent.Properties.ShouldNotBeNull();
        deserializedEvent.Properties.Count.ShouldBe(0);
    }

    [Fact]
    public void SerializeAndDeserialize_StubDomainEventWithProperties_PreservesAllProperties()
    {
        // Arrange
        var originalValue = Guid.NewGuid();
        var originalEvent = new StubDomainEvent(originalValue);
        var now = DateTime.UtcNow;
        originalEvent.Properties["Key1"] = "Value1";
        originalEvent.Properties["Key2"] = 42;
        originalEvent.Properties["Key3"] = now;
        var json = JsonSerializer.Serialize(originalEvent, this.options);

        // Act
        var deserializedEvent = JsonSerializer.Deserialize<StubDomainEvent>(json, this.options);

        // Assert
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.Value.ShouldBe(originalValue);
        deserializedEvent.EventId.ShouldNotBe(Guid.Empty);
        deserializedEvent.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-10), DateTimeOffset.UtcNow.AddSeconds(10));
        deserializedEvent.Properties.ShouldNotBeNull();
        deserializedEvent.Properties.Count.ShouldBe(3);
        deserializedEvent.Properties["Key1"].ShouldBe("Value1");
        deserializedEvent.Properties["Key2"].ShouldBe(42);
        //deserializedEvent.Properties["Key3"].ShouldBe(now);
    }

    [Fact]
    public void SerializeAndDeserialize_NullProperties_PreservesState()
    {
        // Arrange
        var originalValue = Guid.NewGuid();
        var originalEvent = new StubDomainEvent(originalValue)
        {
            //Properties = null
        };
        var json = JsonSerializer.Serialize(originalEvent, this.options);

        // Act
        var deserializedEvent = JsonSerializer.Deserialize<StubDomainEvent>(json, this.options);

        // Assert
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent.Value.ShouldBe(originalValue);
        deserializedEvent.EventId.ShouldNotBe(Guid.Empty);
        deserializedEvent.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        deserializedEvent.Properties.ShouldNotBeNull();
    }

    [Fact]
    public void SerializeAndDeserialize_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = @"{""invalid"": ""data""}"; // No matching properties

        // Act & Assert
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<StubDomainEvent>(json, this.options));
    }
}

public partial class StubDomainEvent(Guid value) : DomainEventBase
{
    // source code generator adds private ctor needed for deserialization

    public Guid Value { get; private set; } = value;
}