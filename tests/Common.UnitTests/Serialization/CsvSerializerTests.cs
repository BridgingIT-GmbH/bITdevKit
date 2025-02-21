// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using CsvHelper.Configuration;
using Shouldly;
using Xunit;

public sealed class PersonStubMap : ClassMap<PersonStub>
{
    public PersonStubMap()
    {
        this.Map(m => m.FirstName);
        this.Map(m => m.LastName);
        this.Map(m => m.Nationality);
        this.Map(m => m.Email)
            .Convert(args => EmailAddressStub.Create(args.Row.GetField("Email"))) // Read
            .Convert(args => args.Value?.Email?.Value); // Write
        this.Map(m => m.Age);
    }
}

public class CsvSerializerTests
{
    private readonly CsvSerializer serializer;

    public CsvSerializerTests()
    {
        var settings = new CsvSerializerSettings();
        this.serializer = new CsvSerializer(settings);
        this.serializer.RegisterClassMap<PersonStubMap>();
    }

    [Fact]
    public void Serialize_WithValidPerson_ShouldCreateValidCsv()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 30);
        using var stream = new MemoryStream();

        // Act
        this.serializer.Serialize(person, stream);
        stream.Position = 0;
        var result = this.ReadStreamContent(stream);

        // Assert
        result.ShouldContain("FirstName;LastName;Nationality;Email;Age");
        result.ShouldContain("John;Doe;USA;john@example.com;30");
    }

    [Fact]
    public void Serialize_WithNullInput_ShouldReturnEmpty()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        this.serializer.Serialize(null, stream);

        // Assert
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public void Serialize_WithCustomDateTimeFormat_ShouldFormatCorrectly()
    {
        // Arrange
        var settings = new CsvSerializerSettings { DateTimeFormat = "dd.MM.yyyy" };
        var serializer = new CsvSerializer(settings);
        serializer.RegisterClassMap<PersonStubMap>();

        var testObject = new { Date = new DateTime(2024, 1, 1) };
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(testObject, stream);
        stream.Position = 0;
        var result = this.ReadStreamContent(stream);

        // Assert
        result.ShouldContain("01.01.2024");
    }

    [Fact]
    public void Deserialize_WithValidCsv_ShouldReturnPerson()
    {
        // Arrange
        const string csv = "FirstName;LastName;Nationality;Email;Age\nJohn;Doe;USA;john@example.com;30";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = this.serializer.Deserialize<PersonStub>(stream);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.Nationality.ShouldBe("USA");
        result.Email.Value.ShouldBe("john@example.com");
        result.Age.ShouldBe(30);
    }

    [Fact]
    public void Deserialize_WithNullStream_ShouldReturnDefault()
    {
        // Act
        var result = this.serializer.Deserialize<PersonStub>(null);

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void Deserialize_WithCustomHeaderMapping_ShouldMapCorrectly()
    {
        // Arrange
        var settings = new CsvSerializerSettings
        {
            HeaderMappings = new Dictionary<string, string>
            {
                { "First", "FirstName" },
                { "Last", "LastName" }
            }
        };
        var serializer = new CsvSerializer(settings);
        serializer.RegisterClassMap<PersonStubMap>();

        const string csv = "First;Last;Nationality;Email;Age\nJohn;Doe;USA;john@example.com;30";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = serializer.Deserialize<PersonStub>(stream);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
    }

    [Fact]
    public void Serialize_WithMultiplePersons_ShouldCreateValidCsv()
    {
        // Arrange
        var persons = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 30),
            new PersonStub("Jane", "Smith", "jane@example.com", 25)
        };
        using var stream = new MemoryStream();

        // Act
        this.serializer.Serialize(persons, stream);
        stream.Position = 0;
        var result = this.ReadStreamContent(stream);

        // Assert
        result.ShouldContain("FirstName;LastName;Nationality;Email;Age");
        result.ShouldContain("John;Doe;USA;john@example.com;30");
        result.ShouldContain("Jane;Smith;USA;jane@example.com;25");
    }

    [Fact]
    public void Serialize_WithCommaDelimiter_ShouldUseCorrectSeparator()
    {
        // Arrange
        var settings = new CsvSerializerSettings { Delimiter = "," };
        var serializer = new CsvSerializer(settings);
        serializer.RegisterClassMap<PersonStubMap>();

        var person = new PersonStub("John", "Doe", "john@example.com", 30);
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(person, stream);
        stream.Position = 0;
        var result = this.ReadStreamContent(stream);

        // Assert
        result.ShouldContain("FirstName,LastName,Nationality,Email,Age");
        result.ShouldContain("John,Doe,USA,john@example.com,30");
    }

    [Fact]
    public void Deserialize_WithInvalidData_ShouldThrowSerializationException()
    {
        // Arrange
        const string csv = "InvalidHeader\nInvalidData";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act & Assert
        Should.Throw<SerializationException>(() =>
            this.serializer.Deserialize<PersonStub>(stream));
    }

    [Fact]
    public void Serialize_WithSpecialCharacters_ShouldHandleEscaping()
    {
        // Arrange
        var person = new PersonStub("John;", "Doe\"", "john@example.com", 30);
        using var stream = new MemoryStream();

        // Act
        this.serializer.Serialize(person, stream);
        stream.Position = 0;
        var result = this.ReadStreamContent(stream);

        // Assert
        result.ShouldContain("\"John;\"");
        result.ShouldContain("\"Doe\"\"\"");
        result.ShouldNotContain("﻿"); // No BOM
    }

    [Fact]
    public void Deserialize_WithDifferentCulture_ShouldHandleNumbersCorrectly()
    {
        // Arrange
        var settings = new CsvSerializerSettings { Culture = new CultureInfo("de-DE") };
        var serializer = new CsvSerializer(settings);
        serializer.RegisterClassMap<PersonStubMap>();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "FirstName;LastName;Nationality;Email;Age\nJohn;Doe;USA;john@example.com;30"));

        // Act
        var result = serializer.Deserialize<PersonStub>(stream);

        // Assert
        result.ShouldNotBeNull();
        result.Age.ShouldBe(30);
    }

    private string ReadStreamContent(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
        return reader.ReadToEnd();
    }
}