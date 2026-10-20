// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

[UnitTest("Common")]
public class CborBufferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(1048576)]
    public void Serialize_ByteAndStreamAdapters_ProduceIdenticalOwnedPayloads(int length)
    {
        ISerializer sut = new CborSerializer();
        var value = new byte[length];
        new Random(42).NextBytes(value);
        using var stream = new MemoryStream();

        var bytes = sut.SerializeToBytes(value);
        sut.Serialize(value, stream);
        var generic = sut.Deserialize<byte[]>(bytes);
        var runtime = (byte[])sut.Deserialize(bytes, typeof(byte[]));

        bytes.ShouldBe(stream.ToArray());
        generic.ShouldBe(value);
        runtime.ShouldBe(value);
        sut.SerializeToBytes(42).ShouldBe(new byte[] { 0x18, 0x2A });
        bytes.ShouldBe(stream.ToArray());
    }

    [Fact]
    public void Deserialize_ByteBuffer_DoesNotAliasInputOrOtherResults()
    {
        ISerializer sut = new CborSerializer();
        var encoded = sut.SerializeToBytes(new byte[] { 1, 2, 3 });

        var first = sut.Deserialize<byte[]>(encoded);
        var second = (byte[])sut.Deserialize(encoded, typeof(byte[]));
        encoded[^1] = 9;
        first[0] = 8;

        second.ShouldBe(new byte[] { 1, 2, 3 });
        first.ShouldBe(new byte[] { 8, 2, 3 });
    }

    [Fact]
    public void Deserialize_NullByteArray_PreservesArgumentValidation()
    {
        ISerializer sut = new CborSerializer();

        Should.Throw<ArgumentNullException>(() => sut.Deserialize<byte[]>((byte[])null));
        Should.Throw<ArgumentNullException>(() => sut.Deserialize((byte[])null, typeof(byte[])));
    }

    [Fact]
    public void Deserialize_EmptyByteArray_ReturnsNullForReferenceTypes()
    {
        ISerializer sut = new CborSerializer();

        sut.Deserialize<byte[]>(Array.Empty<byte>()).ShouldBeNull();
        sut.Deserialize(Array.Empty<byte>(), typeof(byte[])).ShouldBeNull();
    }

    [Fact]
    public void Serialize_OutputFailure_DoesNotAffectNextOperation()
    {
        ISerializer sut = new CborSerializer();
        using var output = new FailingWriteStream();

        Should.Throw<IOException>(() => sut.Serialize(new byte[65536], output));
        var bytes = sut.SerializeToBytes(new[] { 1, 2, 3 });

        sut.Deserialize<int[]>(bytes).ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Serialize_MixedConcurrentBuffers_PreservesEveryPayload()
    {
        ISerializer sut = new CborSerializer();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
        {
            var value = new byte[index % 2 == 0 ? 65536 : 32];
            new Random(index).NextBytes(value);
            var bytes = sut.SerializeToBytes(value);
            using var stream = new MemoryStream();
            sut.Serialize(value, stream);

            sut.Deserialize<byte[]>(bytes).ShouldBe(value);
            sut.Deserialize<byte[]>(stream).ShouldBe(value);
            bytes.ShouldBe(stream.ToArray());
        })));
    }

    [Fact]
    public void Serialize_CustomSubclass_PreservesItsSerializerImplementation()
    {
        ISerializer sut = new CustomCborSerializer();

        sut.SerializeToBytes(42).ShouldBe(new byte[] { 7 });
        sut.Deserialize<int>(new byte[] { 7 }).ShouldBe(8);
        sut.Deserialize(new byte[] { 7 }, typeof(int)).ShouldBe(8);
        sut.Deserialize<int>("Bw==").ShouldBe(8);
    }

    private sealed class CustomCborSerializer : CborSerializer, ISerializer
    {
        void ISerializer.Serialize(object value, Stream output) => output.WriteByte(7);
        object ISerializer.Deserialize(Stream input, Type type) => 8;
        T ISerializer.Deserialize<T>(Stream input) => (T)(object)8;
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("Fixture write failure.");
    }
}