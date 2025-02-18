namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System.Text.Json;
using DevKit.Domain.Model;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationtJsonConverter2Tests
{
    private readonly JsonSerializerOptions options;

    public EnumerationtJsonConverter2Tests()
    {
        this.options = new JsonSerializerOptions { WriteIndented = true };
        this.options.Converters.Add(new EnumerationJsonConverter<StubUserRoles2, StubRoleDetails>());
    }

    [Fact]
    public void Serialization_ShouldProduceCorrectJson()
    {
        // Arrange
        var person = new StubPerson2 { Name = "John Doe", Age = 30, Roles = StubUserRoles2.Moderator };

        // Act
        var json = JsonSerializer.Serialize(person, this.options);

        // Assert
        json.ShouldBe("""
                      {
                        "Name": "John Doe",
                        "Age": 30,
                        "Roles": 2
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
                              "Roles": 3
                            }
                            """;

        // Act
        var person = JsonSerializer.Deserialize<StubPerson2>(json, this.options);

        // Assert
        person.ShouldNotBeNull();
        person.Name.ShouldBe("Jane Smith");
        person.Age.ShouldBe(25);
        person.Roles.ShouldBe(StubUserRoles2.Administrator);
        person.Roles.Value.CanModerate.ShouldBeTrue();
    }

    [Fact]
    public void SerializationAndDeserialization_ShouldPreserveAllValues()
    {
        // Arrange
        var originalPerson = new StubPerson2 { Name = "Alice Johnson", Age = 35, Roles = StubUserRoles2.Moderator };

        // Act
        var json = JsonSerializer.Serialize(originalPerson, this.options);
        var deserializedPerson = JsonSerializer.Deserialize<StubPerson2>(json, this.options);

        // Assert
        deserializedPerson.ShouldNotBeNull();
        deserializedPerson.Name.ShouldBe(originalPerson.Name);
        deserializedPerson.Age.ShouldBe(originalPerson.Age);
        deserializedPerson.Roles.ShouldBe(originalPerson.Roles);
        deserializedPerson.Roles.Value.ShouldBe(originalPerson.Roles.Value);
        deserializedPerson.Roles.Value.CanModerate.ShouldBe(originalPerson.Roles.Value.CanModerate);
    }

    [Fact]
    public void Deserialization_WithInvalidEnumerationValue_ShouldThrowException()
    {
        // Arrange
        const string json = """
                            {
                              "Name": "Invalid Person",
                              "Age": 40,
                              "Roles": 4
                            }
                            """;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
                JsonSerializer.Deserialize<StubPerson2>(json, this.options))
            .Message.ShouldContain("is not a valid id for");
    }

    [Fact]
    public void Serialization_WithNullEnumerationValue_ShouldProduceNullForStatus()
    {
        // Arrange
        var person = new StubPerson2 { Name = "Null Status Person", Age = 45, Roles = null };

        // Act
        var json = JsonSerializer.Serialize(person, this.options);

        // Assert
        json.ShouldBe("""
                      {
                        "Name": "Null Status Person",
                        "Age": 45,
                        "Roles": null
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
                              "Roles": null
                            }
                            """;

        // Act
        var person = JsonSerializer.Deserialize<StubPerson2>(json, this.options);

        // Assert
        person.ShouldNotBeNull();
        person.Name.ShouldBe("Null Status Person");
        person.Age.ShouldBe(45);
        person.Roles.ShouldBeNull();
    }
}

public class StubPerson2
{
    public string Name { get; set; }

    public int Age { get; set; }

    public StubUserRoles2 Roles { get; set; }
}

public class StubUserRoles2(int id, StubRoleDetails value)
    : Enumeration<StubRoleDetails>(id, value)
{
    public static StubUserRoles2 User = new(1, new StubRoleDetails("User", false, false));
    public static StubUserRoles2 Moderator = new(2, new StubRoleDetails("Moderator", true, false));
    public static StubUserRoles2 Administrator = new(3, new StubRoleDetails("Administrator", true, true));
}