namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using Application.Messaging;

[UnitTest("Application")]
public class MessageBaseTests
{
    private readonly Faker faker = new();

    [Fact]
    public void Constructor_WithoutId_GeneratesId()
    {
        // Arrange & Act
        var sut = new TestMessage();

        // Assert
        sut.MessageId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(sut.MessageId, out _)
            .ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithId_SetsProvidedId()
    {
        // Arrange
        var id = this.faker.Random.AlphaNumeric(10);

        // Act
        var sut = new TestMessage(id);

        // Assert
        sut.MessageId.ShouldBe(id);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsTimestamp()
    {
        // Arrange & Act
        var sut = new TestMessage();

        // Assert
        sut.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Constructor_WhenCalled_InitializesProperties()
    {
        // Arrange & Act
        var sut = new TestMessage();

        // Assert
        sut.Properties.ShouldNotBeNull();
        sut.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenCalled_ReturnsEmptyValidationResult()
    {
        // Arrange
        var sut = new TestMessage();

        // Act
        var result = sut.Validate();

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrue()
    {
        // Arrange
        var id = this.faker.Random.AlphaNumeric(10);
        var sut = new TestMessage(id);
        var other = new TestMessage(id);

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        // Arrange
        var sut = new TestMessage(this.faker.Random.AlphaNumeric(10));
        var other = new TestMessage(this.faker.Random.AlphaNumeric(10));

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var sut = new TestMessage();

        // Act
        var result = sut.Equals(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
    {
        // Arrange
        var sut = new TestMessage();

        // Act
        var hashCode1 = sut.GetHashCode();
        var hashCode2 = sut.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_ForDifferentMessages_ReturnsDifferentValues()
    {
        // Arrange
        var sut1 = new TestMessage();
        var sut2 = new TestMessage();

        // Act
        var hashCode1 = sut1.GetHashCode();
        var hashCode2 = sut2.GetHashCode();

        // Assert
        hashCode1.ShouldNotBe(hashCode2);
    }

    private class TestMessage : MessageBase
    {
        public TestMessage() { }

        public TestMessage(string id)
        {
            this.MessageId = id;
        }
    }
}