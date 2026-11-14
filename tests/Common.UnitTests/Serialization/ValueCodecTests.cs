// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text;
using Microsoft.Extensions.DependencyInjection;

public class ValueCodecTests
{
    [Fact]
    public async Task Json_RoundTrip_UsesForwardOnlyStreamsAndLeavesThemOpen()
    {
        var sut = new SystemTextJsonValueCodec<ProductCard>();
        var expected = new ProductCard("P-42", 12.50m);
        using var destination = new ForwardOnlyStream(new MemoryStream());

        var written = await sut.EncodeAsync(expected, destination);
        using var source = new ForwardOnlyStream(new MemoryStream(destination.Bytes));
        var result = await sut.DecodeAsync(source);

        written.IsSuccess.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
        destination.Disposed.ShouldBeFalse();
        source.Disposed.ShouldBeFalse();
        sut.Capabilities.CanReadForwardOnly.ShouldBeTrue();
        sut.Capabilities.RequiresSeekableInput.ShouldBeFalse();
    }

    [Fact]
    public async Task Json_Null_IsSuccessfulEncodedValue()
    {
        var sut = new SystemTextJsonValueCodec<ProductCard>();
        using var destination = new MemoryStream();

        var written = await sut.EncodeAsync(null, destination);
        using var source = new ForwardOnlyStream(new MemoryStream(destination.ToArray()));
        var result = await sut.DecodeAsync(source);

        written.IsSuccess.ShouldBeTrue();
        Encoding.UTF8.GetString(destination.ToArray()).ShouldBe("null");
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("{\"code\":\"P\",\"price\":1} false")]
    public async Task Json_IncompleteOrTrailingContent_ReturnsFailure(string input)
    {
        var sut = new SystemTextJsonValueCodec<ProductCard>();
        using var source = new ForwardOnlyStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));

        var result = await sut.DecodeAsync(source);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ValueCodecError);
        source.Disposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Json_CancelledOperation_PropagatesCancellation()
    {
        var sut = new SystemTextJsonValueCodec<ProductCard>();
        using var source = new ForwardOnlyStream(new MemoryStream("null"u8.ToArray()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await sut.DecodeAsync(source, cancellation.Token));
        source.BytesRead.ShouldBe(0);
    }

    [Fact]
    public async Task SerializerAdapter_NonSeekableInput_RejectsBeforeConsumption()
    {
        var sut = new SerializerValueCodec<ProductCard>(new SystemTextJsonSerializer(), new("legacy-json", 1));
        using var source = new ForwardOnlyStream(new MemoryStream("null"u8.ToArray()));

        var result = await sut.DecodeAsync(source);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<ValueCodecError>().Single().Code.ShouldBe("UnsupportedAccess");
        source.BytesRead.ShouldBe(0);
        sut.Capabilities.UsesNativeAsyncIO.ShouldBeFalse();
        sut.Capabilities.RequiresSeekableInput.ShouldBeTrue();
    }

    [Fact]
    public async Task SerializerAdapter_SeekableInput_RoundTripsWithoutReplacingSerializer()
    {
        var serializer = new SystemTextJsonSerializer();
        var sut = new SerializerValueCodec<ProductCard>(serializer, new("legacy-json", 1));
        using var destination = new MemoryStream();
        var expected = new ProductCard("P-1", 2m);

        var written = await sut.EncodeAsync(expected, destination);
        destination.Position = 0;
        var result = await sut.DecodeAsync(destination);

        written.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
        destination.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task SerializerAdapter_RequiredSeekableOutput_RejectsBeforeWriting()
    {
        var sut = new SerializerValueCodec<ProductCard>(new SystemTextJsonSerializer(), new("legacy-json", 1), requiresSeekableOutput: true);
        using var destination = new ForwardOnlyStream(new MemoryStream());

        var result = await sut.EncodeAsync(new("P-1", 1m), destination);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<ValueCodecError>().Single().Code.ShouldBe("UnsupportedAccess");
        destination.Bytes.Length.ShouldBe(0);
    }

    [Fact]
    public void Registry_ExplicitReaders_DoesNotReplaceHostSerializer()
    {
        var serializer = new SystemTextJsonSerializer();
        var codec = new SystemTextJsonValueCodec<ProductCard>();
        var services = new ServiceCollection();
        services.AddSingleton<ISerializer>(serializer);
        services.AddValueCodec(codec);
        using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<ValueCodecRegistry>();

        sut.TryGet<ProductCard>(new("json", 1), out var found).ShouldBeTrue();
        found.ShouldBeSameAs(codec);
        sut.TryGet<ProductCard>(new("json", 2), out _).ShouldBeFalse();
        sut.TryGet<string>(new("json", 1), out _).ShouldBeFalse();
        provider.GetRequiredService<ISerializer>().ShouldBeSameAs(serializer);
    }

    [Fact]
    public void Registry_DuplicateContractFormat_FailsInsteadOfSelectingArbitrarily()
    {
        IValueCodecRegistration[] registrations =
        [
            new ValueCodecRegistration<ProductCard>(new SystemTextJsonValueCodec<ProductCard>()),
            new ValueCodecRegistration<ProductCard>(new SystemTextJsonValueCodec<ProductCard>())
        ];

        Should.Throw<ArgumentException>(() => new ValueCodecRegistry(registrations));
    }

    [Theory]
    [InlineData("7", true)]
    [InlineData("77", false)]
    [InlineData("x", false)]
    [InlineData("", false)]
    public async Task ExplicitValidator_ConsumesCompleteBoundedInput(string input, bool valid)
    {
        var sut = new DigitValidator();
        using var source = new ForwardOnlyStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));

        var result = await sut.ValidateAsync(source);

        result.IsSuccess.ShouldBe(valid);
        source.Disposed.ShouldBeFalse();
        source.BytesRead.ShouldBe(input.Length);
    }

    public sealed record ProductCard(string Code, decimal Price);

    private sealed class DigitValidator : IValueCodecValidator<int>
    {
        public ValueCodecFormat Format => new("digit", 1);

        public ValueCodecCapabilities Capabilities { get; } = new(true, true, false, false, true, 2);

        public async ValueTask<Result> ValidateAsync(Stream source, CancellationToken cancellationToken = default)
        {
            var buffer = new byte[2];
            var count = await source.ReadAtLeastAsync(buffer, 2, false, cancellationToken);
            return count == 1 && buffer[0] is >= (byte)'0' and <= (byte)'9'
                ? Result.Success()
                : Result.Failure(new ValueCodecError("InvalidPayload", "One decimal digit is required."));
        }
    }

    private sealed class ForwardOnlyStream(Stream inner) : Stream
    {
        public bool Disposed { get; private set; }

        public int BytesRead { get; private set; }

        public byte[] Bytes => ((MemoryStream)inner).ToArray();

        public override bool CanRead => !this.Disposed && inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => !this.Disposed && inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            this.BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            this.BytesRead += read;
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
