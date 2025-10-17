namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System.Text.Json;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationJsonConverterTests
{
    private readonly JsonSerializerOptions options;

    public EnumerationJsonConverterTests()
    {
        this.options = new JsonSerializerOptions { WriteIndented = true };
        this.options.Converters.Add(new EnumerationJsonConverter<StubStatus, int, string>());
    }

    [Fact]
    public void Serialization_ShouldProduceCorrectJson()
    {
        // Arrange
        var person = new StubPerson { Name = "John Doe", Age = 30, Status = StubStatus.Stub02 };

        // Act
        var json = JsonSerializer.Serialize(person, this.options);

        // Assert
        json.ShouldBe("""
                      {
                        "Name": "John Doe",
                        "Age": 30,
                        "Status": 2
                      }
                      """);
    }

    [Fact]
    public void Deserialization_ShouldRecreateCorrectObject()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Jane Smith",
                              "Age": 25,
                              "Status": 3
                            }
                            """;

        // Act
        var person = JsonSerializer.Deserialize<StubPerson>(json, this.options);

        // Assert
        person.ShouldNotBeNull();
        person.Name.ShouldBe("Jane Smith");
        person.Age.ShouldBe(25);
        person.Status.ShouldBe(StubStatus.Stub03);
        person.Status.Code.ShouldBe("S3");
        person.Status.Description.ShouldBe("Lorem Ipsum03");
    }

    [Fact]
    public void SerializationAndDeserialization_ShouldPreserveAllValues()
    {
        // Arrange
        var originalPerson = new StubPerson { Name = "Alice Johnson", Age = 35, Status = StubStatus.Stub01 };

        // Act
        var json = JsonSerializer.Serialize(originalPerson, this.options);
        var deserializedPerson = JsonSerializer.Deserialize<StubPerson>(json, this.options);

        // Assert
        deserializedPerson.ShouldNotBeNull();
        deserializedPerson.Name.ShouldBe(originalPerson.Name);
        deserializedPerson.Age.ShouldBe(originalPerson.Age);
        deserializedPerson.Status.ShouldBe(originalPerson.Status);
        deserializedPerson.Status.Id.ShouldBe(originalPerson.Status.Id);
        deserializedPerson.Status.Value.ShouldBe(originalPerson.Status.Value);
        deserializedPerson.Status.Code.ShouldBe(originalPerson.Status.Code);
        deserializedPerson.Status.Description.ShouldBe(originalPerson.Status.Description);
    }

    [Fact]
    public void Deserialization_WithInvalidEnumerationValue_ShouldThrowException()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Invalid Person",
                              "Age": 40,
                              "Status": 4
                            }
                            """;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
                JsonSerializer.Deserialize<StubPerson>(json, this.options))
            .Message.ShouldContain("is not a valid id for");
    }

    [Fact]
    public void Serialization_WithNullEnumerationValue_ShouldProduceNullForStatus()
    {
        // Arrange
        var person = new StubPerson { Name = "Null Status Person", Age = 45, Status = null };

        // Act
        var json = JsonSerializer.Serialize(person, this.options);

        // Assert
        json.ShouldBe("""
                      {
                        "Name": "Null Status Person",
                        "Age": 45,
                        "Status": null
                      }
                      """);
    }

    [Fact]
    public void Deserialization_WithNullEnumerationValue_ShouldProduceNullStatus()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Null Status Person",
                              "Age": 45,
                              "Status": null
                            }
                            """;

        // Act
        var person = JsonSerializer.Deserialize<StubPerson>(json, this.options);

        // Assert
        person.ShouldNotBeNull();
        person.Name.ShouldBe("Null Status Person");
        person.Age.ShouldBe(45);
        person.Status.ShouldBeNull();
    }
}

public class StubPerson
{
    public string Name { get; set; }

    public int Age { get; set; }

    public StubStatus Status { get; set; }
}