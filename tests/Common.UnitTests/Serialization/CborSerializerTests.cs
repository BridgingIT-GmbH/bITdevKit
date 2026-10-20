// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Formats.Cbor;
using System.Collections.Concurrent;
using BridgingIT.DevKit.Domain.Model;
using System.Dynamic;
using System.Numerics;

[UnitTest("Common")]
public class CborSerializerTests(ITestOutputHelper output) : SerializerTestsBase(output)
{
    [Fact]
    public override void CanRoundTripStream_Test()
    {
        base.CanRoundTripStream_Test();
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

    [Fact]
    public void Serialize_PrimitiveValues_WritesKnownCborEncoding()
    {
        // Arrange
        var sut = this.GetSerializer();

        // Act
        var bytes = sut.SerializeToBytes(new[] { 1, 2, 3 });

        // Assert
        Convert.ToHexString(bytes).ShouldBe("83010203");
    }

    [Fact]
    public void Serialize_ByteArray_WritesNativeByteString()
    {
        // Arrange
        var sut = this.GetSerializer();

        // Act
        var bytes = sut.SerializeToBytes(new byte[] { 1, 2, 3 });

        // Assert
        Convert.ToHexString(bytes).ShouldBe("43010203");
        sut.Deserialize<byte[]>(bytes).ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void SerializeToString_Value_ReturnsBase64()
    {
        // Arrange
        var sut = this.GetSerializer();

        // Act
        var text = sut.SerializeToString(new[] { 1, 2, 3 });

        // Assert
        text.ShouldBe("gwECAw==");
        sut.Deserialize<int[]>(text).ShouldBe(new[] { 1, 2, 3 });
        ((int[])sut.Deserialize(text, typeof(int[]))).ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Serialize_NullValue_LeavesOutputUnchanged()
    {
        // Arrange
        var sut = this.GetSerializer();
        using var stream = new MemoryStream();
        stream.WriteByte(42);

        // Act
        sut.Serialize(null, stream);

        // Assert
        stream.ToArray().ShouldBe(new byte[] { 42 });
    }

    [Fact]
    public void Serialize_NullOutput_DoesNotThrow()
    {
        // Arrange
        var sut = this.GetSerializer();

        // Act / Assert
        Should.NotThrow(() => sut.Serialize(42, null));
    }

    [Fact]
    public void Serialize_OutputStream_RemainsOpenAtEndOfPayload()
    {
        // Arrange
        var sut = this.GetSerializer();
        using var stream = new MemoryStream();
        stream.WriteByte(0);

        // Act
        sut.Serialize(42, stream);

        // Assert
        stream.CanWrite.ShouldBeTrue();
        stream.Position.ShouldBe(stream.Length);
        Convert.ToHexString(stream.ToArray()).ShouldBe("00182A");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deserialize_NullOrEmptyInput_ReturnsDefault(bool empty)
    {
        // Arrange
        var sut = this.GetSerializer();
        using var stream = empty ? new MemoryStream() : null;

        // Act / Assert
        sut.Deserialize(stream, typeof(int)).ShouldBeNull();
        sut.Deserialize<int>(stream).ShouldBe(0);
        sut.Deserialize<StubModel>(stream).ShouldBeNull();
    }

    [Fact]
    public void Deserialize_NullType_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = this.GetSerializer();

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => sut.Deserialize((Stream)null, null))
            .ParamName.ShouldBe("type");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deserialize_SeekableInput_RewindsAndLeavesStreamOpen(bool generic)
    {
        // Arrange
        var sut = this.GetSerializer();
        using var stream = new MemoryStream(Convert.FromHexString("83010203"));
        stream.Position = stream.Length;

        // Act
        var values = generic ? sut.Deserialize<int[]>(stream) : (int[])sut.Deserialize(stream, typeof(int[]));

        // Assert
        values.ShouldBe(new[] { 1, 2, 3 });
        stream.CanRead.ShouldBeTrue();
        stream.Position.ShouldBe(stream.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deserialize_NonSeekableInput_ReadsCurrentPosition(bool generic)
    {
        // Arrange
        var sut = this.GetSerializer();
        using var stream = new NonSeekableStream(Convert.FromHexString("0083010203"));
        stream.ReadByte().ShouldBe(0);

        // Act
        var values = generic ? sut.Deserialize<int[]>(stream) : (int[])sut.Deserialize(stream, typeof(int[]));

        // Assert
        values.ShouldBe(new[] { 1, 2, 3 });
        stream.CanRead.ShouldBeTrue();
    }

    [Theory]
    [InlineData("FF")]
    [InlineData("830102")]
    public void Deserialize_MalformedInput_ThrowsCborContentException(string hex)
    {
        // Arrange
        var sut = this.GetSerializer();

        // Act / Assert
        Should.Throw<CborContentException>(() => sut.Deserialize<int[]>(Convert.FromHexString(hex)));
    }

    [Fact]
    public void Serialize_ObjectMembers_UsesClrNames()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = new StubModel { IntProperty = 42 };

        // Act
        var bytes = sut.SerializeToBytes(value);
        var properties = sut.Deserialize<Dictionary<string, object>>(bytes);

        // Assert
        properties.ShouldContainKey("IntProperty");
        properties.ShouldNotContainKey("intProperty");
        sut.Deserialize<StubModel>(bytes).IntProperty.ShouldBe(42);
    }

    [Fact]
    public void Serialize_NestedValues_RoundTripsScalarTypesAndCollections()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = new Dictionary<string, decimal[]> { ["prices"] = [12.50m, -3.25m] };

        // Act
        var actual = sut.Deserialize<Dictionary<string, decimal[]>>(sut.SerializeToBytes(value));

        // Assert
        actual["prices"].ShouldBe(value["prices"]);
    }

    [Theory]
    [InlineData("43010203")]
    [InlineData("5F4101420203FF")]
    public void Deserialize_NativeByteString_ReturnsByteArray(string hex)
    {
        var sut = this.GetSerializer();

        var actual = sut.Deserialize<byte[]>(Convert.FromHexString(hex));

        actual.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Theory]
    [InlineData("9F010203FF")]
    [InlineData("83010203")]
    public void Deserialize_DefiniteOrIndefiniteArray_ReadsValues(string hex)
    {
        var sut = this.GetSerializer();

        sut.Deserialize<int[]>(Convert.FromHexString(hex)).ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Deserialize_IndefiniteMapAndText_ReadsValues()
    {
        var sut = this.GetSerializer();

        // { "name": (_ "Goo", "se") }
        var actual = sut.Deserialize<Dictionary<string, string>>(Convert.FromHexString("BF646E616D657F63476F6F627365FFFF"));

        actual["name"].ShouldBe("Goose");
    }

    [Theory]
    [InlineData("F93E00")]
    [InlineData("FA3FC00000")]
    [InlineData("FB3FF8000000000000")]
    public void Deserialize_NativeFloatingPoint_ReadsNumber(string hex)
    {
        var sut = this.GetSerializer();

        sut.Deserialize<double>(Convert.FromHexString(hex)).ShouldBe(1.5d);
    }

    [Theory]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("123456789.12345678901234567890")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335")]
    public void Serialize_PreciseDecimal_RoundTripsWithoutLoss(string text)
    {
        var sut = this.GetSerializer();
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        var actual = sut.Deserialize<decimal>(sut.SerializeToBytes(value));

        actual.ShouldBe(value);
    }

    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(1.1e-28)]
    public void Serialize_FloatingPointExtremes_RoundTripsWithoutLoss(double value)
    {
        var sut = this.GetSerializer();

        sut.Deserialize<double>(sut.SerializeToBytes(value)).ShouldBe(value);
    }

    [Fact]
    public void Serialize_IntegerBoundaries_RoundTripsWithoutLoss()
    {
        var sut = this.GetSerializer();

        sut.Deserialize<long>(sut.SerializeToBytes(long.MinValue)).ShouldBe(long.MinValue);
        sut.Deserialize<ulong>(sut.SerializeToBytes(ulong.MaxValue)).ShouldBe(ulong.MaxValue);
        sut.Deserialize<Int128>(sut.SerializeToBytes(Int128.MinValue)).ShouldBe(Int128.MinValue);
        sut.Deserialize<UInt128>(sut.SerializeToBytes(UInt128.MaxValue)).ShouldBe(UInt128.MaxValue);
        // Native major type 1 can also represent negative values below Int64.MinValue.
        sut.Deserialize<decimal>(Convert.FromHexString("3BFFFFFFFFFFFFFFFF")).ShouldBe(-18446744073709551616m);
    }

    [Fact]
    public void Serialize_UntypedValues_RoundTripsNullsBooleansDatesAndUnicode()
    {
        var sut = this.GetSerializer();
        var value = new Dictionary<string, object>
        {
            ["empty"] = null,
            ["enabled"] = true,
            ["disabled"] = false,
            ["text"] = "Grüße 世界",
            ["date"] = DateTimeOffset.Parse("2026-10-20T14:30:00+02:00", System.Globalization.CultureInfo.InvariantCulture)
        };

        var actual = sut.Deserialize<Dictionary<string, object>>(sut.SerializeToBytes(value));

        actual["empty"].ShouldBeNull();
        actual["enabled"].ShouldBe(true);
        actual["disabled"].ShouldBe(false);
        actual["text"].ShouldBe("Grüße 世界");
        actual["date"].ShouldBe(value["date"]);
    }

    [Theory]
    [InlineData("0102")]
    [InlineData("A2616101616102")]
    [InlineData("C4822001FF")]
    [InlineData("C48100")]
    public void Deserialize_TrailingDataDuplicateKeysOrMalformedTag_ThrowsCborContentException(string hex)
    {
        var sut = this.GetSerializer();

        Should.Throw<CborContentException>(() => sut.Deserialize<object>(Convert.FromHexString(hex)));
    }

    [Theory]
    [InlineData("D86401")]
    public void Deserialize_UnsupportedTag_ThrowsNotSupportedException(string hex)
    {
        var sut = this.GetSerializer();

        Should.Throw<NotSupportedException>(() => sut.Deserialize<object>(Convert.FromHexString(hex)));
    }

    [Fact]
    public void Deserialize_ExcessiveDepth_ThrowsNotSupportedException()
    {
        ISerializer sut = new CborSerializer(maxDepth: 2);

        Should.Throw<NotSupportedException>(() => sut.Deserialize<object>(Convert.FromHexString("81818101")));
    }

    [Fact]
    public void Serialize_NestedBinaryValues_PreservesNativeBytesAndOrdinaryStrings()
    {
        var sut = this.GetSerializer();
        var value = new BinaryModel
        {
            Bytes = [1, 2, 3],
            Text = "AQID",
            Child = new BinaryModel { Bytes = [4, 5] },
            Attachments = new Dictionary<string, byte[]> { ["file"] = [6, 7] },
            Any = new byte[] { 8, 9 }
        };

        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<BinaryModel>(bytes);
        var untyped = sut.Deserialize<Dictionary<string, object>>(bytes);

        actual.Bytes.ShouldBe(value.Bytes);
        actual.Child.Bytes.ShouldBe(value.Child.Bytes);
        actual.Attachments["file"].ShouldBe(value.Attachments["file"]);
        actual.Any.ShouldBeOfType<byte[]>().ShouldBe(new byte[] { 8, 9 });
        actual.Text.ShouldBe("AQID");
        untyped["Bytes"].ShouldBeOfType<byte[]>().ShouldBe(value.Bytes);
        untyped["Text"].ShouldBeOfType<string>().ShouldBe("AQID");
    }

    [Fact]
    public void Serialize_LargeBinaryValue_StoresPayloadWithoutBase64Expansion()
    {
        var sut = this.GetSerializer();
        var value = new byte[100_000];
        new Random(42).NextBytes(value);

        var bytes = sut.SerializeToBytes(value);

        bytes.Length.ShouldBe(value.Length + 5);
        sut.Deserialize<byte[]>(bytes).ShouldBe(value);
    }

    [Fact]
    public void Serialize_ImmutableRecordAndStruct_RoundTripsConstructorAndMembers()
    {
        var sut = this.GetSerializer();
        var value = new ImmutableModel(42, "record", [1, 2, 3]);
        var point = new PointModel { X = 1, Y = 2 };

        var actual = sut.Deserialize<ImmutableModel>(sut.SerializeToBytes(value));
        var actualPoint = sut.Deserialize<PointModel>(sut.SerializeToBytes(point));

        actual.Id.ShouldBe(value.Id);
        actual.Name.ShouldBe(value.Name);
        actual.Bytes.ShouldBe(value.Bytes);
        actualPoint.X.ShouldBe(1);
        actualPoint.Y.ShouldBe(2);
    }

    [Fact]
    public void Serialize_CollectionsEnumsAndNullableValues_RoundTripsTypedMembers()
    {
        var sut = this.GetSerializer();
        var value = new ScalarModel
        {
            Id = Guid.NewGuid(),
            Date = new DateTime(2026, 10, 20, 12, 30, 0, DateTimeKind.Utc),
            DateOnly = new DateOnly(2026, 10, 20),
            TimeOnly = new TimeOnly(12, 30, 45),
            Duration = TimeSpan.FromTicks(12345),
            Status = TestStatus.Ready,
            Optional = 42,
            Values = new HashSet<int> { 1, 2 },
            Lookup = new Dictionary<int, byte[]> { [42] = [3, 4] }
        };

        var actual = sut.Deserialize<ScalarModel>(sut.SerializeToBytes(value));

        actual.Id.ShouldBe(value.Id);
        actual.Date.ShouldBe(value.Date);
        actual.Date.Kind.ShouldBe(DateTimeKind.Utc);
        actual.DateOnly.ShouldBe(value.DateOnly);
        actual.TimeOnly.ShouldBe(value.TimeOnly);
        actual.Duration.ShouldBe(value.Duration);
        actual.Status.ShouldBe(value.Status);
        actual.Optional.ShouldBe(42);
        actual.Absent.ShouldBeNull();
        actual.Values.Order().ShouldBe(value.Values.Order());
        actual.Lookup[42].ShouldBe(value.Lookup[42]);
        sut.Deserialize<IReadOnlyList<int>>(sut.SerializeToBytes(new[] { 1, 2 })).ShouldBe(new[] { 1, 2 });
        sut.Deserialize<Stack<int>>(sut.SerializeToBytes(new Stack<int>(new[] { 1, 2 }))).ShouldBe(new[] { 2, 1 });
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.0)]
    public void Serialize_SpecialFloatingPoint_PreservesBits(double value)
    {
        var sut = this.GetSerializer();
        var actual = sut.Deserialize<double>(sut.SerializeToBytes(value));

        if (double.IsNaN(value)) double.IsNaN(actual).ShouldBeTrue();
        else BitConverter.DoubleToInt64Bits(actual).ShouldBe(BitConverter.DoubleToInt64Bits(value));
    }

    [Fact]
    public void Deserialize_UnknownNestedMembers_SkipsValuesAndEnforcesDepth()
    {
        var sut = this.GetSerializer();
        var bytes = sut.SerializeToBytes(new Dictionary<string, object>
        {
            ["IntProperty"] = 42,
            ["Unknown"] = new Dictionary<string, object> { ["nested"] = new[] { 1, 2 } },
            [new string('X', 512)] = true
        });

        sut.Deserialize<StubModel>(bytes).IntProperty.ShouldBe(42);
        Should.Throw<NotSupportedException>(() => new CborSerializer(maxDepth: 2).Deserialize<StubModel>(bytes));
    }

    [Fact]
    public void Serialize_CyclicGraph_ThrowsAndLeavesSerializerReusable()
    {
        var sut = this.GetSerializer();
        var value = new BinaryModel();
        value.Child = value;

        Should.Throw<NotSupportedException>(() => sut.SerializeToBytes(value));
        sut.Deserialize<int>(sut.SerializeToBytes(42)).ShouldBe(42);
    }

    [Fact]
    public async Task Serialize_ConcurrentCalls_RoundTripsWithoutSharedOperationState()
    {
        var sut = this.GetSerializer();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var value = new BinaryModel { Bytes = BitConverter.GetBytes(i), Text = i.ToString() };
            var actual = sut.Deserialize<BinaryModel>(sut.SerializeToBytes(value));
            actual.Bytes.ShouldBe(value.Bytes);
            actual.Text.ShouldBe(value.Text);
        })));
    }

    private sealed class BinaryModel
    {
        public byte[] Bytes { get; set; }
        public string Text { get; set; }
        public BinaryModel Child { get; set; }
        public Dictionary<string, byte[]> Attachments { get; set; }
        public object Any { get; set; }
    }

    private sealed record ImmutableModel(int Id, string Name, byte[] Bytes);

    private struct PointModel
    {
        public int X { get; set; }
        public int Y;
    }

    private enum TestStatus : ushort { Ready = 300 }

    private sealed class ScalarModel
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public DateOnly DateOnly { get; set; }
        public TimeOnly TimeOnly { get; set; }
        public TimeSpan Duration { get; set; }
        public TestStatus Status { get; set; }
        public int? Optional { get; set; }
        public int? Absent { get; set; }
        public HashSet<int> Values { get; set; }
        public Dictionary<int, byte[]> Lookup { get; set; }
    }

    [Fact]
    public void Serialize_ReadonlyStructAndLegacyCollections_RoundTripsValues()
    {
        var sut = this.GetSerializer();
        var value = new MoneyModel(12.50m);
        var legacy = new System.Collections.Hashtable { ["data"] = new byte[] { 1, 2 } };
        var collection = new CustomIntCollection { 1, 2 };

        sut.Deserialize<MoneyModel>(sut.SerializeToBytes(value)).Amount.ShouldBe(12.50m);
        sut.Deserialize<System.Collections.Hashtable>(sut.SerializeToBytes(legacy))["data"]
            .ShouldBeOfType<byte[]>().ShouldBe(new byte[] { 1, 2 });
        sut.Deserialize<CustomIntCollection>(sut.SerializeToBytes(collection)).ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public void Serialize_MultidimensionalArray_ThrowsNotSupportedException()
    {
        var sut = this.GetSerializer();

        Should.Throw<NotSupportedException>(() => sut.SerializeToBytes(new int[2, 2]));
    }

    private readonly struct MoneyModel(decimal amount)
    {
        public decimal Amount { get; } = amount;
    }

    private sealed class CustomIntCollection : System.Collections.ObjectModel.Collection<int> { }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Serialize_DateTime_PreservesTicksAndKind(DateTimeKind kind)
    {
        var sut = this.GetSerializer();
        var value = new DateTime(2026, 10, 20, 12, 30, 45, kind).AddTicks(12345);

        var actual = sut.Deserialize<DateTime>(sut.SerializeToBytes(value));

        actual.Ticks.ShouldBe(value.Ticks);
        actual.Kind.ShouldBe(kind);
    }

    [Fact]
    public void Deserialize_ConstructorDefault_DistinguishesMissingFromNull()
    {
        var sut = this.GetSerializer();
        var explicitNull = sut.SerializeToBytes(new Dictionary<string, object> { ["Value"] = null });
        var missing = sut.SerializeToBytes(new Dictionary<string, object>());

        sut.Deserialize<OptionalModel>(explicitNull).Value.ShouldBeNull();
        sut.Deserialize<OptionalModel>(missing).Value.ShouldBe(42);
    }

    private sealed class OptionalModel(int? value = 42)
    {
        public int? Value { get; } = value;
    }

    [Fact]
    public void Serialize_ConcurrentStack_PreservesPopOrder()
    {
        var sut = this.GetSerializer();
        var value = new ConcurrentStack<int>(new[] { 1, 2, 3 });

        var actual = sut.Deserialize<ConcurrentStack<int>>(sut.SerializeToBytes(value));

        while (value.TryPop(out var expected))
        {
            actual.TryPop(out var item).ShouldBeTrue();
            item.ShouldBe(expected);
        }

        actual.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Serialize_LegacyStack_PreservesPopOrder()
    {
        var sut = this.GetSerializer();
        var value = new System.Collections.Stack(new[] { "first", "second", "third" });

        var actual = sut.Deserialize<System.Collections.Stack>(sut.SerializeToBytes(value));

        while (value.Count > 0)
        {
            actual.Pop().ShouldBe(value.Pop());
        }

        actual.Count.ShouldBe(0);
    }

    [Fact]
    public void Serialize_HiddenInheritedProperties_PrefersMostDerivedMember()
    {
        var sut = this.GetSerializer();
        var value = new DerivedHiddenMemberModel { Id = Guid.NewGuid(), BaseOnly = "inherited" };
        ((BaseHiddenMemberModel)value).Id = 42;
        ((MiddleHiddenMemberModel)value).Id = "hidden";

        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<DerivedHiddenMemberModel>(bytes);
        var properties = sut.Deserialize<Dictionary<string, object>>(bytes);

        actual.Id.ShouldBe(value.Id);
        actual.BaseOnly.ShouldBe("inherited");
        properties["Id"].ShouldBe(value.Id);
        properties.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_EntityWithHiddenIdentifier_WritesMostDerivedIdentifier(bool auditable)
    {
        var sut = this.GetSerializer();
        var id = Guid.NewGuid();
        Entity<SerializationEntityId> value;
        if (auditable)
        {
            value = new HiddenIdAuditableEntity { Id = new SerializationEntityId(id) };
        }
        else
        {
            value = new HiddenIdEntity { Id = new SerializationEntityId(id) };
        }

        value.Id = new SerializationEntityId(Guid.NewGuid());

        var properties = sut.Deserialize<Dictionary<string, object>>(sut.SerializeToBytes(value));

        properties["Id"].ShouldBeOfType<Dictionary<object, object>>()["Value"].ShouldBe(id);
    }

    [Theory]
    [InlineData("A26158017F6158FF02")]
    [InlineData("A27F6158FF01615802")]
    [InlineData("A261580178015802")]
    public void Deserialize_DuplicateDecodedMemberNames_ThrowsCborContentException(string hex)
    {
        var sut = this.GetSerializer();
        var bytes = Convert.FromHexString(hex);

        Should.Throw<CborContentException>(() => sut.Deserialize<DuplicateMemberModel>(bytes));
        Should.Throw<CborContentException>(() => sut.Deserialize(bytes, typeof(DuplicateMemberModel)));
        Should.Throw<CborContentException>(() => sut.Deserialize<ReadOnlyDuplicateMemberModel>(bytes));
        Should.Throw<CborContentException>(() => sut.Deserialize<ConstructorDuplicateMemberModel>(bytes));
        Should.Throw<CborContentException>(() => sut.Deserialize<UnknownDuplicateMemberModel>(bytes));
        Should.Throw<CborContentException>(() => sut.Deserialize<Dictionary<string, int>>(bytes));
    }

    [Fact]
    public void Deserialize_LongDuplicateDecodedUnknownNames_ThrowsCborContentException()
    {
        var sut = this.GetSerializer();
        var name = new string('X', 512);
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(2);
        writer.WriteTextString(name);
        writer.WriteInt32(1);
        writer.WriteStartIndefiniteLengthTextString();
        writer.WriteTextString(name[..256]);
        writer.WriteTextString(name[256..]);
        writer.WriteEndIndefiniteLengthTextString();
        writer.WriteInt32(2);
        writer.WriteEndMap();

        Should.Throw<CborContentException>(() => sut.Deserialize<UnknownDuplicateMemberModel>(writer.Encode()));
    }

    private class BaseHiddenMemberModel
    {
        public int Id { get; set; }
        public string BaseOnly { get; set; }
    }

    private class MiddleHiddenMemberModel : BaseHiddenMemberModel
    {
        public new string Id { get; set; }
    }

    private sealed class DerivedHiddenMemberModel : MiddleHiddenMemberModel
    {
        public new Guid Id { get; set; }
    }

    private sealed class SerializationEntityId(Guid value) : EntityId<Guid>
    {
        public override Guid Value { get; protected set; } = value;

        protected override IEnumerable<object> GetAtomicValues()
        {
            return [this.Value];
        }
    }

    private sealed class HiddenIdEntity : Entity<SerializationEntityId, Guid> { }

    private sealed class HiddenIdAuditableEntity : AuditableEntity<SerializationEntityId, Guid> { }

    private sealed class DuplicateMemberModel
    {
        public int X { get; set; }
    }

    private sealed class ReadOnlyDuplicateMemberModel
    {
        public int X => 0;
    }

    private sealed record ConstructorDuplicateMemberModel(int X);

    private sealed class UnknownDuplicateMemberModel { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_DefaultOnlyExpandoObject_RoundTripsEntries(bool empty)
    {
        var sut = this.GetSerializer();
        var value = new ExpandoObject();
        var entries = (IDictionary<string, object>)value;
        if (!empty)
        {
            entries.Add("name", "fixture");
            entries.Add("count", 42L);
            entries.Add("bytes", new byte[] { 1, 2, 3 });
            entries.Add("optional", null);
        }

        var bytes = sut.SerializeToBytes(value);
        var copies = new[]
        {
            sut.Deserialize<ExpandoObject>(bytes),
            (ExpandoObject)sut.Deserialize(bytes, typeof(ExpandoObject)),
            sut.Deserialize<ExpandoPayloadModel>(sut.SerializeToBytes(new ExpandoPayloadModel { Payload = value })).Payload
        };

        foreach (IDictionary<string, object> actual in copies)
        {
            actual.Count.ShouldBe(entries.Count);
            if (!empty)
            {
                actual["name"].ShouldBe("fixture");
                actual["count"].ShouldBe(42L);
                actual["bytes"].ShouldBeOfType<byte[]>().ShouldBe(new byte[] { 1, 2, 3 });
                actual.ShouldContainKey("optional");
                actual["optional"].ShouldBeNull();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_DefaultOnlyHostFeatures_RoundTripsEntriesAndComparer(bool empty)
    {
        var sut = this.GetSerializer();
        var value = new HostFeatureEndpointCollection();
        if (!empty)
        {
            value.Add("consoleCommands", new HostFeatureEndpointMetadata
            {
                ProtocolVersion = 2,
                Transport = "named-pipe",
                Endpoint = "fixture-console",
                Nonce = "fixture-nonce"
            });
        }

        var bytes = sut.SerializeToBytes(value);
        var copies = new[]
        {
            sut.Deserialize<HostFeatureEndpointCollection>(bytes),
            (HostFeatureEndpointCollection)sut.Deserialize(bytes, typeof(HostFeatureEndpointCollection)),
            sut.Deserialize<HostRuntimeDescriptor>(sut.SerializeToBytes(new HostRuntimeDescriptor { Features = value })).Features
        };

        foreach (var actual in copies)
        {
            actual.Count.ShouldBe(value.Count);
            actual.Comparer.Equals("feature", "FEATURE").ShouldBeTrue();
            if (!empty)
            {
                var endpoint = actual["CONSOLECOMMANDS"];
                endpoint.ProtocolVersion.ShouldBe(2);
                endpoint.Transport.ShouldBe("named-pipe");
                endpoint.Endpoint.ShouldBe("fixture-console");
                endpoint.Nonce.ShouldBe("fixture-nonce");
            }
        }
    }

    [Fact]
    public void Serialize_DefaultOnlyLegacyDictionary_RoundTripsEntries()
    {
        var sut = this.GetSerializer();
        var value = new DefaultOnlyHashtable { ["bytes"] = new byte[] { 1, 2, 3 } };

        var actual = sut.Deserialize<DefaultOnlyHashtable>(sut.SerializeToBytes(value));

        actual.Count.ShouldBe(1);
        actual["bytes"].ShouldBeOfType<byte[]>().ShouldBe(new byte[] { 1, 2, 3 });
    }

    private sealed class ExpandoPayloadModel
    {
        public ExpandoObject Payload { get; set; }
    }

    private sealed class DefaultOnlyHashtable : System.Collections.Hashtable { }

    [Fact]
    public void Serialize_InheritedPrivateSetters_RoundTripsValues()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = new DerivedPrivateSetterModel();
        value.Update(42, "inherited");

        // Act
        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<DerivedPrivateSetterModel>(bytes);
        var runtime = (DerivedPrivateSetterModel)sut.Deserialize(bytes, typeof(DerivedPrivateSetterModel));

        // Assert
        actual.Value.ShouldBe(42);
        actual.Label.ShouldBe("inherited");
        runtime.Value.ShouldBe(42);
        runtime.Label.ShouldBe("inherited");
    }

    [Fact]
    public void Serialize_ExplicitParameterlessReadonlyStruct_RoundTripsConstructorValues()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = new ExplicitParameterlessReadonlyStruct();

        // Act
        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<ExplicitParameterlessReadonlyStruct>(bytes);
        var runtime = (ExplicitParameterlessReadonlyStruct)sut.Deserialize(bytes, typeof(ExplicitParameterlessReadonlyStruct));

        // Assert
        actual.Value.ShouldBe(42);
        actual.Label.ShouldBe("initialized");
        runtime.Value.ShouldBe(42);
        runtime.Label.ShouldBe("initialized");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deserialize_ExplicitParameterlessStruct_PreservesDefaultsForMissingMembers(bool populated)
    {
        // Arrange
        var sut = this.GetSerializer();
        var members = new Dictionary<string, object>();
        if (populated)
        {
            members.Add("Value", 9);
            members.Add("Label", null);
        }

        var bytes = sut.SerializeToBytes(members);

        // Act
        var actual = sut.Deserialize<ExplicitParameterlessMutableStruct>(bytes);

        // Assert
        actual.Value.ShouldBe(populated ? 9 : 42);
        actual.Label.ShouldBe(populated ? null : "initialized");
        actual.Field.ShouldBe(7);
    }

    private class BasePrivateSetterModel
    {
        public int Value { get; private set; } = -1;
        public string Label { get; private set; } = "default";

        public void Update(int value, string label)
        {
            this.Value = value;
            this.Label = label;
        }
    }

    private class MiddlePrivateSetterModel : BasePrivateSetterModel { }

    private sealed class DerivedPrivateSetterModel : MiddlePrivateSetterModel { }

    private readonly struct ExplicitParameterlessReadonlyStruct
    {
        public ExplicitParameterlessReadonlyStruct()
        {
            this.Value = 42;
        }

        public int Value { get; }
        public string Label { get; } = "initialized";
    }

    private struct ExplicitParameterlessMutableStruct
    {
        public ExplicitParameterlessMutableStruct()
        {
            this.Value = 42;
        }

        public int Value { get; set; }
        public string Label { get; set; } = "initialized";
        public int Field = 7;
    }

    protected override ISerializer GetSerializer()
    {
        return new CborSerializer();
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}