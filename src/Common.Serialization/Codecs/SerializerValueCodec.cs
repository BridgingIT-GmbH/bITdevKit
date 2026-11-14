// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Adapts an existing synchronous serializer while preserving its real access requirements.</summary>
/// <typeparam name="T">The caller-selected contract.</typeparam>
/// <param name="serializer">The existing serializer, whose registration is not changed.</param>
/// <param name="format">Its explicitly assigned persisted format.</param>
/// <param name="requiresSeekableInput">Whether decoding requires seeking/Length, true for the existing JSON serializer.</param>
/// <param name="maximumScratchBytes">An optional documented materialization/scratch estimate.</param>
/// <param name="requiresSeekableOutput">Whether the serializer also requires seekable output.</param>
/// <example><code>var codec = new SerializerValueCodec&lt;ProductCard&gt;(serializer, new("custom", 1));</code></example>
public sealed class SerializerValueCodec<T>(
    ISerializer serializer,
    ValueCodecFormat format,
    bool requiresSeekableInput = true,
    long? maximumScratchBytes = null,
    bool requiresSeekableOutput = false) : IValueCodec<T>
{
    private readonly ISerializer serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <inheritdoc />
    public string FormatId { get; } = !string.IsNullOrWhiteSpace(format.Id)
        ? format.Id : throw new ArgumentException("A stable format identifier is required.", nameof(format));

    /// <inheritdoc />
    public int FormatVersion { get; } = format.Version > 0
        ? format.Version : throw new ArgumentOutOfRangeException(nameof(format));

    /// <inheritdoc />
    public ValueCodecCapabilities Capabilities { get; } = new(!requiresSeekableInput, !requiresSeekableOutput, requiresSeekableInput, true, false,
        maximumScratchBytes is >= 0 or null ? maximumScratchBytes : throw new ArgumentOutOfRangeException(nameof(maximumScratchBytes)));

    /// <inheritdoc />
    public ValueTask<Result> EncodeAsync(T value, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (!destination.CanWrite || (!this.Capabilities.CanWriteForwardOnly && !destination.CanSeek))
        {
            return ValueTask.FromResult(Result.Failure(new ValueCodecError("UnsupportedAccess", "The destination does not support the serializer's required access.")));
        }

        try
        {
            this.serializer.Serialize(value, destination);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result.Success());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ValueTask.FromResult(Result.Failure(new ValueCodecError("EncodingFailed", "The selected serializer could not encode the contract.")));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<T>> DecodeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.CanRead || (this.Capabilities.RequiresSeekableInput && !source.CanSeek))
        {
            return ValueTask.FromResult(Result<T>.Failure(new ValueCodecError("UnsupportedAccess", "The selected serializer requires readable, seekable input.")));
        }

        try
        {
            var value = this.serializer.Deserialize<T>(source);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result<T>.Success(value));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ValueTask.FromResult(Result<T>.Failure(new ValueCodecError("DecodingFailed", "The selected serializer could not decode the contract.")));
        }
    }
}