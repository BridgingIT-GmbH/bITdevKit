// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text.Json;
using Shouldly;
using Xunit;

public class UniversalContractResolverTests
{
    private readonly JsonSerializerOptions options;

    public UniversalContractResolverTests()
    {
        options = new JsonSerializerOptions
        {
            TypeInfoResolver = new UniversalContractResolver(),
            PropertyNameCaseInsensitive = true
        };
    }

    // Test class with public constructor and public setters
    public class TestPublicClass
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public TestPublicClass() { }
    }

    // Test class with private constructor and private setters
    public class TestPrivateClass
    {
        public int Id { get; private set; }
        public string Name { get; private set; }

        private TestPrivateClass() { }
    }

    // Test class with protected constructor and mixed setters
    public class TestProtectedClass
    {
        public int Id { get; protected set; }
        public string Name { get; private set; }

        protected TestProtectedClass() { }
    }

    // Test the StubStatus class with inherited properties
    public class StubStatusTest : Enumeration<int, string>
    {
        public static readonly StubStatusTest Stub01 = new(1, "Stub01", "S1", "Lorem Ipsum01");
        public static readonly StubStatusTest Stub03 = new(3, "Stub03", "S3", "Lorem Ipsum03");

        private StubStatusTest() { }

        private StubStatusTest(int id, string value, string code, string description) : base(id, value)
        {
            Code = code;
            Description = description;
        }

        public string Code { get; private set; }
        public string Description { get; private set; }
    }

    // Test class with primitive types (Guid, DateTime) and optional types
    public class TestPrimitiveClass
    {
        public Guid GuidValue { get; private set; }
        public DateTime DateTimeValue { get; private set; }
        public int? OptionalInt { get; private set; }
        public Guid? OptionalGuid { get; private set; }

        private TestPrimitiveClass() { }
    }

    // Test class with child objects
    public class TestParentClass
    {
        public int ParentId { get; private set; }
        public TestChildClass Child { get; private set; }

        private TestParentClass() { }
    }

    public class TestChildClass
    {
        public string ChildName { get; private set; }
        public int ChildValue { get; private set; }

        private TestChildClass() { }
    }

    [Fact]
    public void Deserialize_PublicClassWithPublicSetters_SetsPropertiesCorrectly()
    {
        var json = @"{""id"": 1, ""name"": ""TestName""}";
        var result = JsonSerializer.Deserialize<TestPublicClass>(json, options);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Name.ShouldBe("TestName");
    }

    [Fact]
    public void Deserialize_PrivateClassWithPrivateSetters_SetsPropertiesCorrectly()
    {
        var json = @"{""id"": 2, ""name"": ""PrivateTest""}";
        var result = JsonSerializer.Deserialize<TestPrivateClass>(json, options);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(2);
        result.Name.ShouldBe("PrivateTest");
    }

    [Fact]
    public void Deserialize_ProtectedClassWithMixedSetters_SetsPropertiesCorrectly()
    {
        var json = @"{""id"": 3, ""name"": ""ProtectedTest""}";
        var result = JsonSerializer.Deserialize<TestProtectedClass>(json, options);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(3);
        result.Name.ShouldBe("ProtectedTest");
    }

    [Fact]
    public void Deserialize_StubStatusWithInheritedProperties_SetsAllPropertiesCorrectly()
    {
        var json = @"[
            { ""id"": 1, ""value"": ""Stub01"", ""code"": ""S1"", ""description"": ""Lorem Ipsum01"" },
            { ""id"": 3, ""value"": ""Stub03"", ""code"": ""S3"", ""description"": ""Lorem Ipsum03"" }
        ]";
        var result = JsonSerializer.Deserialize<List<StubStatusTest>>(json, options);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe(1);
        result[1].Id.ShouldBe(3);
        result[0].Value.ShouldBe("Stub01");
        result[1].Value.ShouldBe("Stub03");
        result[0].Code.ShouldBe("S1");
        result[1].Code.ShouldBe("S3");
        result[0].Description.ShouldBe("Lorem Ipsum01");
        result[1].Description.ShouldBe("Lorem Ipsum03");
    }

    [Fact]
    public void Deserialize_EmptyObject_SetsDefaults()
    {
        var json = @"{}";
        var result = JsonSerializer.Deserialize<TestPublicClass>(json, options);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(0); // Default int
        result.Name.ShouldBeNull(); // Default string
    }

    [Fact]
    public void Deserialize_PrimitiveClassWithGuidAndDateTime_SetsPropertiesCorrectly()
    {
        var guid = Guid.NewGuid();
        var dateTime = new DateTime(2023, 10, 15, 14, 30, 0, DateTimeKind.Utc);
        var json = $@"{{""guidValue"": ""{guid}"", ""dateTimeValue"": ""2023-10-15T14:30:00Z"", ""optionalInt"": 42, ""optionalGuid"": ""{guid}""}}";
        var result = JsonSerializer.Deserialize<TestPrimitiveClass>(json, options);

        result.ShouldNotBeNull();
        result.GuidValue.ShouldBe(guid);
        result.DateTimeValue.ShouldBe(dateTime);
        result.OptionalInt.ShouldBe(42);
        result.OptionalGuid.ShouldBe(guid);
    }

    [Fact]
    public void Deserialize_PrimitiveClassWithNullOptionalTypes_SetsPropertiesCorrectly()
    {
        var json = @"{""guidValue"": ""550e8400-e29b-41d4-a716-446655440000"", ""dateTimeValue"": ""2023-10-15T14:30:00Z"", ""optionalInt"": null, ""optionalGuid"": null}";
        var result = JsonSerializer.Deserialize<TestPrimitiveClass>(json, options);

        result.ShouldNotBeNull();
        result.GuidValue.ShouldNotBe(Guid.Empty);
        result.DateTimeValue.ShouldBe(new DateTime(2023, 10, 15, 14, 30, 0, DateTimeKind.Utc));
        result.OptionalInt.ShouldBeNull();
        result.OptionalGuid.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ParentClassWithChildObject_SetsPropertiesCorrectly()
    {
        var json = @"{""parentId"": 1, ""child"": {""childName"": ""Child1"", ""childValue"": 100}}";
        var result = JsonSerializer.Deserialize<TestParentClass>(json, options);

        result.ShouldNotBeNull();
        result.ParentId.ShouldBe(1);
        result.Child.ShouldNotBeNull();
        result.Child.ChildName.ShouldBe("Child1");
        result.Child.ChildValue.ShouldBe(100);
    }

    [Fact]
    public void Deserialize_ParentClassWithNullChild_SetsPropertiesCorrectly()
    {
        var json = @"{""parentId"": 2, ""child"": null}";
        var result = JsonSerializer.Deserialize<TestParentClass>(json, options);

        result.ShouldNotBeNull();
        result.ParentId.ShouldBe(2);
        result.Child.ShouldBeNull();
    }
}