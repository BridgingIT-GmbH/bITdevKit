// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text.Json;

[UnitTest("Common")]
public class PropertyBagSerializationTests
{
    [Fact]
    public void Serialize_WithScalars_EmitsExistingJsonByteForByte()
    {
        // Arrange
        const string expected = """{"Count\u003C":"bdk_v1_eyJ0eXBlIjoidWludDE2IiwidmFsdWUiOiI0MiJ9","Signature":"bdk_v1_eyJ0eXBlIjoiYnl0ZXMiLCJ2YWx1ZSI6IkFRSUQifQ","Note":"bdk_v1_eyJ0eXBlIjoibnVsbCIsInZhbHVlIjpudWxsfQ"}""";
        var properties = new PropertyBag { ["Count<"] = (ushort)42, ["Signature"] = new byte[] { 1, 2, 3 }, ["Note"] = null };

        // Act
        var json = PropertyBagSerialization.Serialize(properties);
        var restored = PropertyBagSerialization.Deserialize(expected);

        // Assert
        Encoding.UTF8.GetBytes(json).ShouldBe(Encoding.UTF8.GetBytes(expected));
        restored.Keys.ShouldBe(["Count<", "Signature", "Note"]);
        restored.Get("count<").ShouldBeOfType<ushort>().ShouldBe((ushort)42);
        restored.Get("signature").ShouldBeOfType<byte[]>().ShouldBe([1, 2, 3]);
        restored.TryGetValue("note", out var note).ShouldBeTrue();
        note.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(PropertyBagScalarCodecTests.Values), MemberType = typeof(PropertyBagScalarCodecTests))]
    [InlineData('A')]
    public void Deserialize_WithSupportedScalar_RestoresRuntimeTypeAndValue(object value)
    {
        // Arrange
        var json = PropertyBagSerialization.Serialize(new() { ["Value"] = value });

        // Act
        var restored = PropertyBagSerialization.Deserialize(json);

        // Assert
        restored.TryGetValue("value", out var decoded).ShouldBeTrue();
        if (value is byte[] bytes)
        {
            var decodedBytes = decoded.ShouldBeOfType<byte[]>();
            decodedBytes.ShouldBe(bytes);
            decodedBytes.ShouldNotBeSameAs(bytes);
            var secondRead = PropertyBagSerialization.Deserialize(json).Get("value").ShouldBeOfType<byte[]>();
            decodedBytes[0] = 99;
            secondRead.ShouldBe(bytes);
        }
        else
        {
            decoded.ShouldBe(value);
            decoded?.GetType().ShouldBe(value?.GetType());
        }
    }

    [Fact]
    public void Clone_WithScalarBag_PreservesEntriesAndDetachesByteArrays()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3 };
        var properties = new PropertyBag { ["Signature"] = bytes, ["Count"] = (short)4, ["Note"] = null };

        // Act
        var clone = PropertyBagSerialization.Clone(properties);
        bytes[0] = 99;
        clone.Set("count", (short)5);

        // Assert
        clone.Keys.ShouldBe(["Signature", "Count", "Note"]);
        var clonedBytes = clone.Get("SIGNATURE").ShouldBeOfType<byte[]>();
        clonedBytes.ShouldBe([1, 2, 3]);
        clonedBytes[1] = 98;
        bytes[1].ShouldBe((byte)2);
        properties.Get("Count").ShouldBeOfType<short>().ShouldBe((short)4);
        clone.Get("Count").ShouldBeOfType<short>().ShouldBe((short)5);
        clone.TryGetValue("note", out var note).ShouldBeTrue();
        note.ShouldBeNull();
    }

    [Fact]
    public void CloneAndSerialize_WithNullOrEmptyBag_PreserveTheirRespectiveContracts()
    {
        // Arrange
        var empty = new PropertyBag();

        // Act
        var clone = PropertyBagSerialization.Clone(empty);

        // Assert
        PropertyBagSerialization.Clone(null).ShouldBeNull();
        clone.ShouldBeEmpty();
        clone.ShouldNotBeSameAs(empty);
        PropertyBagSerialization.Serialize(null).ShouldBeNull();
        PropertyBagSerialization.Serialize(empty).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    public void Deserialize_WithEmptyInput_ReturnsFreshEmptyBag(string json)
    {
        // Act
        var first = PropertyBagSerialization.Deserialize(json);
        var second = PropertyBagSerialization.Deserialize(json);

        // Assert
        first.ShouldBeEmpty();
        second.ShouldBeEmpty();
        first.Set("new", true);
        second.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_WithLegacyStrings_PreservesLiteralValuesAndExactDuplicateHandling()
    {
        // Arrange
        const string json = """{"Code":"first","Code":"00123","Approved":"true","Note":null}""";

        // Act
        var restored = PropertyBagSerialization.Deserialize(json);

        // Assert
        restored.Get("code").ShouldBeOfType<string>().ShouldBe("00123");
        restored.Get("approved").ShouldBeOfType<string>().ShouldBe("true");
        restored.TryGetValue("note", out var note).ShouldBeTrue();
        note.ShouldBeNull();
    }

    [Theory]
    [InlineData(" ", typeof(JsonException))]
    [InlineData("{", typeof(JsonException))]
    [InlineData("[]", typeof(JsonException))]
    [InlineData("""{"Count":42}""", typeof(JsonException))]
    [InlineData("null", typeof(ArgumentNullException))]
    [InlineData("""{"Code":"a","code":"b"}""", typeof(ArgumentException))]
    [InlineData("""{"Value":"bdk_v1_*"}""", typeof(FormatException))]
    [InlineData("""{"Value":"bdk_v1_eyJ0eXBlIjoidW5rbm93biIsInZhbHVlIjoieCJ9"}""", typeof(FormatException))]
    public void Deserialize_WithMalformedInput_PropagatesExistingException(string json, Type expectedException)
    {
        // Act
        var exception = Record.Exception(() => PropertyBagSerialization.Deserialize(json));

        // Assert
        exception.ShouldNotBeNull();
        exception.GetType().ShouldBe(expectedException);
    }

    [Fact]
    public void Serialize_WithUnsupportedValue_ThrowsCodecArgumentException()
    {
        // Arrange
        var properties = new PropertyBag { ["Counts"] = new[] { 1 } };

        // Act
        var exception = Should.Throw<ArgumentException>(() => PropertyBagSerialization.Serialize(properties));

        // Assert
        exception.ParamName.ShouldBe("value");
    }
}
