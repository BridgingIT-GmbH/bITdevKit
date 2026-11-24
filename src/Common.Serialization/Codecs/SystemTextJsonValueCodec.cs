// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>Encodes typed JSON with DevKit conventions using asynchronous forward-only BCL I/O.</summary>
/// <typeparam name="T">The explicitly selected value contract.</typeparam>
/// <param name="options">Optional caller-owned JSON conventions, copied before use.</param>
/// <example><code>IValueCodec&lt;ProductCard&gt; codec = new SystemTextJsonValueCodec&lt;ProductCard&gt;();</code></example>
public sealed class SystemTextJsonValueCodec<T>(JsonSerializerOptions options = null) : IValueCodec<T>
{
    private readonly JsonSerializerOptions options = options is null
        ? DefaultJsonSerializerOptions.Create()
        : new JsonSerializerOptions(options);

    /// <inheritdoc />
    public string FormatId => "json";

    /// <inheritdoc />
    public int FormatVersion => 1;

    /// <inheritdoc />
    public ValueCodecCapabilities Capabilities => ValueCodecCapabilities.ForwardOnly;

    /// <inheritdoc />
    public async ValueTask<Result> EncodeAsync(T value, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (!destination.CanWrite)
        {
            return Result.Failure(new ValueCodecError("InvalidStream", "The destination is not writable."));
        }

        try
        {
            await JsonSerializer.SerializeAsync(destination, value, this.options, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (JsonException)
        {
            return Result.Failure(new ValueCodecError("InvalidPayload", "The value cannot be encoded as the declared JSON contract."));
        }
        catch (NotSupportedException)
        {
            return Result.Failure(new ValueCodecError("UnsupportedContract", "The JSON contract requires an explicitly registered converter."));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<T>> DecodeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.CanRead)
        {
            return Result<T>.Failure(new ValueCodecError("InvalidStream", "The source is not readable."));
        }

        try
        {
            var value = await JsonSerializer.DeserializeAsync<T>(source, this.options, cancellationToken).ConfigureAwait(false);
            return Result<T>.Success(value);
        }
        catch (JsonException)
        {
            return Result<T>.Failure(new ValueCodecError("InvalidPayload", "Input is not a complete value of the declared JSON contract."));
        }
        catch (NotSupportedException)
        {
            return Result<T>.Failure(new ValueCodecError("UnsupportedContract", "The JSON contract requires an explicitly registered converter."));
        }
    }
}