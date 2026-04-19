// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using BenchmarkDotNet.Running;
using System.Text.Json.Serialization;

[UnitTest("Common")]
public class SystemTextJsonSerializerTests(ITestOutputHelper output) : SerializerTestsBase(output)
{
    [Fact]
    public override void CanRoundTripStream_Test()
    {
        base.CanRoundTripStream_Test();
    }

    [Fact]
    public override void CanRoundTripStream_Benchmark()
    {
        base.CanRoundTripStream_Benchmark();
    }

    [Fact]
    public override void CanRoundTripPrivateConstructorStream_Test()
    {
        base.CanRoundTripPrivateConstructorStream_Test();
    }

    [Fact]
    public override void CanRoundTripEmptyStream_Test()
    {
        base.CanRoundTripEmptyStream_Test();
    }

    [Fact]
    public override void CanRoundTripBytes_Test()
    {
        base.CanRoundTripBytes_Test();
    }

    [Fact]
    public override void CanRoundTripString_Test()
    {
        base.CanRoundTripString_Test();
    }

    [Fact(Skip = "Skip benchmarks for now")]
    public virtual void RunBenchmarks()
    {
        var summary = BenchmarkRunner.Run<SystemTextJsonSerializerBenchmark>();
    }

    protected override ISerializer GetSerializer()
    {
        return new SystemTextJsonSerializer();
    }

    [Fact]
    public void CanRoundTripNestedPrivateSetterValueObject_Test()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = NestedValueObjectRoot.Create("todo-created", "luke.skywalker@starwars.com");

        // Act
        using var stream = new MemoryStream();
        sut.Serialize(value, stream);
        var actual = sut.Deserialize<NestedValueObjectRoot>(stream);

        // Assert
        actual.ShouldNotBeNull();
        actual.Name.ShouldBe(value.Name);
        actual.Email.ShouldNotBeNull();
        actual.Email.Value.ShouldBe(value.Email.Value);
    }
}

public class SystemTextJsonSerializerBenchmark(ITestOutputHelper output) : SerializerBenchmarkBase(output)
{
    protected override ISerializer GetSerializer()
    {
        return new SystemTextJsonSerializer();
    }
}

public class NestedValueObjectRoot
{
    private NestedValueObjectRoot() { }

    private NestedValueObjectRoot(string name, NestedValueObject email)
    {
        this.Name = name;
        this.Email = email;
    }

    public string Name { get; private set; }

    public NestedValueObject Email { get; private set; }

    public static NestedValueObjectRoot Create(string name, string email)
    {
        return new NestedValueObjectRoot(name, NestedValueObject.Create(email));
    }
}

public class NestedValueObject
{
    private NestedValueObject() { }

    private NestedValueObject(string value)
    {
        this.Value = value;
    }

    [JsonInclude]
    public string Value { get; private set; }

    public static NestedValueObject Create(string value)
    {
        return new NestedValueObject(value);
    }
}
