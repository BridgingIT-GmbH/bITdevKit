// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System.Text.Json; // added for serialization tests
using BridgingIT.DevKit.Common; // added to access DefaultSystemTextJsonSerializerOptions

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationTests
{
    [Fact]
    public void GetAll_ShouldReturnAllEnumerations()
    {
        // Arrange & Act
        var sut = Enumeration.GetAll<StubStatus>()
            .ToArray();

        // Assert
        sut.ShouldNotBeEmpty();
        sut.Length.ShouldBe(3);
        sut.ShouldContain(e => e.Id == 1 && e.Value == "Stub01" && e.Code == "S1");
        sut.ShouldContain(e => e.Id == 2 && e.Value == "Stub02" && e.Code == "S2");
        sut.ShouldContain(e => e.Id == 3 && e.Value == "Stub03" && e.Code == "S3");
    }

    [Fact]
    public void FromId_WithValidId_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = Enumeration.FromId<StubStatus>(2);

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(2);
        sut.Value.ShouldBe("Stub02");
        sut.Code.ShouldBe("S2");
        sut.Description.ShouldBe("Lorem Ipsum02");
    }

    [Fact]
    public void FromId_WithInvalidId_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
        Should.Throw<InvalidOperationException>(() => Enumeration.FromId<StubStatus>(0))
            .Message.ShouldBe("'0' is not a valid id for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.StubStatus");
    }

    [Fact]
    public void FromValue_WithValidName_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = Enumeration.FromValue<StubStatus>("Stub03");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(3);
        sut.Value.ShouldBe("Stub03");
        sut.Code.ShouldBe("S3");
    }

    [Fact]
    public void FromValue_WithValidNameCaseInsensitive_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = Enumeration.FromValue<StubStatus>("STUB03");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(3);
        sut.Value.ShouldBe("Stub03");
        sut.Code.ShouldBe("S3");
    }

    [Fact]
    public void FromValue_WithInvalidName_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
        Should.Throw<InvalidOperationException>(() => Enumeration.FromValue<StubStatus>("Stub00"))
            .Message.ShouldBe("'Stub00' is not a valid value for BridgingIT.DevKit.Domain.UnitTests.Domain.Model.StubStatus");
    }

    [Fact]
    public void Equals_WithSameEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = StubStatus.Stub03;
        var other = StubStatus.Stub03;

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentEnumeration_ShouldReturnFalse()
    {
        // Arrange
        var sut = StubStatus.Stub01;
        var other = StubStatus.Stub03;

        // Act
        var result = sut.Equals(other);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var sut = StubStatus.Stub01;

        // Act
        var result = sut.Equals(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var sut = StubStatus.Stub01;
        var differentType = new object();

        // Act
        var result = sut.Equals(differentType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsOperator_WithSameEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = StubStatus.Stub03;
        var other = StubStatus.Stub03;

        // Act
        var result = sut == other;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsOperator_WithDifferentEnumeration_ShouldReturnFalse()
    {
        // Arrange
        var sut = StubStatus.Stub01;
        var other = StubStatus.Stub03;

        // Act
        var result = sut == other;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void NotEqualsOperator_WithSameEnumeration_ShouldReturnFalse()
    {
        // Arrange
        var sut = StubStatus.Stub03;
        var other = StubStatus.Stub03;

        // Act
        var result = sut != other;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void NotEqualsOperator_WithDifferentEnumeration_ShouldReturnTrue()
    {
        // Arrange
        var sut = StubStatus.Stub01;
        var other = StubStatus.Stub03;

        // Act
        var result = sut != other;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsOperator_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var sut = StubStatus.Stub01;

        // Act
        var result = sut == null;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsOperator_WithNullOnBothSides_ShouldReturnTrue()
    {
        // Arrange
        StubStatus sut = null;
        StubStatus other = null;

        // Act
        var result = sut == other;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistentForSameEnumeration()
    {
        // Arrange
        var sut1 = StubStatus.Stub01;
        var sut2 = StubStatus.Stub01;

        // Act
        var hashCode1 = sut1.GetHashCode();
        var hashCode2 = sut2.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferentForDifferentEnumerations()
    {
        // Arrange
        var sut1 = StubStatus.Stub01;
        var sut2 = StubStatus.Stub02;

        // Act
        var hashCode1 = sut1.GetHashCode();
        var hashCode2 = sut2.GetHashCode();

        // Assert
        hashCode1.ShouldNotBe(hashCode2);
    }

    [Fact]
    public void ToString_ShouldReturnName()
    {
        // Arrange
        var sut = StubStatus.Stub01;

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldBe("Stub01");
    }

    [Fact]
    public void CompareTo_ShouldOrderCorrectly()
    {
        // Arrange
        var sut1 = StubStatus.Stub01;
        var sut2 = StubStatus.Stub02;
        var sut3 = StubStatus.Stub03;

        // Act
        var orderedList = new List<StubStatus> { sut3, sut1, sut2 };
        orderedList.Sort();

        // Assert
        orderedList.ShouldBe([sut1, sut2, sut3]);
    }

    [Fact]
    public void Code_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var sut1 = StubStatus.Stub01;
        var sut2 = StubStatus.Stub02;
        var sut3 = StubStatus.Stub03;

        // Assert
        sut1.Code.ShouldBe("S1");
        sut2.Code.ShouldBe("S2");
        sut3.Code.ShouldBe("S3");
    }

    [Fact]
    public void Description_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var sut = StubStatus.Stub01;

        // Assert
        sut.Description.ShouldBe("Lorem Ipsum01");
    }

    [Fact]
    public void GetByCode_WithValidCode_ShouldReturnCorrectEnumeration()
    {
        // Arrange & Act
        var sut = StubStatus.GetByCode("S2");

        // Assert
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(2);
        sut.Value.ShouldBe("Stub02");
        sut.Code.ShouldBe("S2");
        sut.Description.ShouldBe("Lorem Ipsum02");
    }

    [Fact]
    public void GetByCode_WithInvalidCode_ShouldReturnNull()
    {
        // Arrange & Act
        var sut = StubStatus.GetByCode("InvalidCode");

        // Assert
        sut.ShouldBeNull();
    }

    [Fact]
    public void GetByCode_WithNullCode_ShouldReturnNull()
    {
        // Arrange & Act
        var sut = StubStatus.GetByCode(null);

        // Assert
        sut.ShouldBeNull();
    }

    // -------------------------------------------------
    // JSON Serialization / Deserialization (System.Text)
    // -------------------------------------------------

    [Fact]
    public void Json_Serialize_StubStatus_ShouldContainExpectedFields()
    {
        // Arrange
        var options = DefaultJsonSerializerOptions.Create();
        var sut = StubStatus.Stub02;

        // Act
        var json = JsonSerializer.Serialize(sut, options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        root.GetProperty("id").GetInt32().ShouldBe(2);
        root.GetProperty("value").GetString().ShouldBe("Stub02");
        root.GetProperty("code").GetString().ShouldBe("S2");
        root.GetProperty("description").GetString().ShouldBe("Lorem Ipsum02");
    }

    [Fact]
    public void Json_Deserialize_KnownId_MinimalJson_ShouldEqualStaticInstanceAndLeaveMissingPropsNull()
    {
        // Arrange
        var options = DefaultJsonSerializerOptions.Create();
        var json = """{"id":3,"value":"Stub03"}"""; // minimal JSON

        // Act
        var status = JsonSerializer.Deserialize<StubStatus>(json, options);

        // Assert
        status.ShouldNotBeNull();
        status.Id.ShouldBe(3);
        status.Value.ShouldBe("Stub03");
        // Missing in JSON -> remain null
        status.Code.ShouldBeNull();
        status.Description.ShouldBeNull();
        (status == StubStatus.Stub03).ShouldBeTrue();
    }

    [Fact]
    public void Json_Deserialize_FullJson_KnownId_ShouldPopulatePrivateSetProperties()
    {
        // Arrange
        var options = DefaultJsonSerializerOptions.Create();
        var json = """{"id":1,"value":"Stub01","code":"S1","description":"Lorem Ipsum X"}""";

        // Act
        var status = JsonSerializer.Deserialize<StubStatus>(json, options);

        // Assert
        status.ShouldNotBeNull();
        status.Id.ShouldBe(1);
        status.Value.ShouldBe("Stub01");
        status.Code.ShouldBe("S1");
        status.Description.ShouldBe("Lorem Ipsum X"); // value from JSON overrides static instance metadata (different description)
        (status == StubStatus.Stub01).ShouldBeTrue(); // equality by type + id only
    }

    [Fact]
    public void Json_Roundtrip_List_ShouldPreserveIds()
    {
        // Arrange
        var options = DefaultJsonSerializerOptions.Create();
        var list = new[] { StubStatus.Stub01, StubStatus.Stub03 };

        // Act
        var json = JsonSerializer.Serialize(list, options);
        var result = JsonSerializer.Deserialize<List<StubStatus>>(json, options);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe(1);
        result[1].Id.ShouldBe(3);
        (result[0] == StubStatus.Stub01).ShouldBeTrue();
        (result[1] == StubStatus.Stub03).ShouldBeTrue();
    }

    [Fact]
    public void Json_Deserialize_UnknownId_ShouldCreateDistinctInstance()
    {
        // Arrange
        var options = DefaultJsonSerializerOptions.Create();
        var json = """{"id":99,"value":"Custom"}""";

        // Act
        var status = JsonSerializer.Deserialize<StubStatus>(json, options);

        // Assert
        status.ShouldNotBeNull();
        status.Id.ShouldBe(99);
        status.Value.ShouldBe("Custom");
        status.Code.ShouldBeNull();
        status.Description.ShouldBeNull();
        Enumeration.GetAll<StubStatus>().Any(s => s.Id == 99).ShouldBeFalse();
    }
}

public partial class StubStatus : Enumeration
{
    public static readonly StubStatus Stub01 = new(1, "Stub01", "S1", "Lorem Ipsum01", "help1");
    public static readonly StubStatus Stub02 = new(2, "Stub02", "S2", "Lorem Ipsum02", "help2");
    public static readonly StubStatus Stub03 = new(3, "Stub03", "S3", "Lorem Ipsum03", "help3");

    //private StubStatus(int id, string value, string code, string description, string helpText = null)
    //    : base(id, value)
    //{
    //    this.Code = code;
    //    this.Description = description;
    //    this.HelpText = helpText;
    //}

    public string Code { get; private set; }

    public string Description { get; private set; }

    public string HelpText { get; private set; }

    public static StubStatus GetByCode(string code)
    {
        return GetAll()
            .FirstOrDefault(e => e.Code == code);
    }
}

//public partial class StubStatus // source generator
//{
//    private StubStatus() // for json deserialization
//    {
//    }

//    /// <summary>
//    ///  Retrieves all defined <see cref="StubStatus"/> instances.
//    /// </summary>
//    /// <returns>All defined <see cref="StubStatus"/> instances.</returns>
//    public static IEnumerable<StubStatus> GetAll()
//    {
//        return GetAll<StubStatus>();
//    }

//    /// <summary>
//    ///   Retrieves a <see cref="StubStatus"/> instance by its unique identifier.
//    /// </summary>
//    /// <param name="id">The unique identifier of the status.</param>
//    /// <returns>The <see cref="StubStatus"/> instance associated with the given <paramref name="id"/>.</returns>
//    public static StubStatus GetById(int id)
//    {
//        return GetAll<StubStatus>()
//            .FirstOrDefault(e => e.Id == id) ?? throw new ArgumentException($"No StubStatus exists with Id {id}", nameof(id));
//    }

//    /// <summary>
//    /// Implicitly converts an <see cref="int"/> identifier into a corresponding
//    /// <see cref="StubStatus"/> if one exists.
//    /// </summary>
//    /// <param name="id">The identifier of the customer status.</param>
//    /// <exception cref="ArgumentException">
//    /// Thrown if no customer status is defined with the specified <paramref name="id"/>.
//    /// </exception>
//    /// <returns>The <see cref="StubStatus"/> instance associated with the given <paramref name="id"/>.</returns>
//    public static implicit operator StubStatus(int id)
//    {
//        return GetAll<StubStatus>().FirstOrDefault(e => e.Id == id)
//            ?? throw new ArgumentException($"No StubStatus exists with Id {id}", nameof(id));
//    }
//}