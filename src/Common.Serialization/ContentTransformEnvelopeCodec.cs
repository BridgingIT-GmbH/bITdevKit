// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
/// Encodes versioned content-transform envelopes into provider-safe text.
/// </summary>
/// <example>
/// <code>
/// var encoded = ContentTransformEnvelopeCodec.Encode(envelope);
/// var decoded = ContentTransformEnvelopeCodec.Decode(encoded);
/// </code>
/// </example>
public static class ContentTransformEnvelopeCodec
{
    /// <summary>Gets the persisted envelope prefix.</summary>
    public const string Prefix = "bdk_ct1_";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Encodes an envelope.</summary>
    /// <param name="envelope">The envelope.</param>
    /// <returns>The provider-safe text.</returns>
    /// <example><code>var encoded = ContentTransformEnvelopeCodec.Encode(envelope);</code></example>
    public static string Encode(ContentTransformEnvelope envelope)
    {
        Validate(envelope);
        return Prefix + Base64UrlHelper.Encode(JsonSerializer.SerializeToUtf8Bytes(ToDto(envelope), JsonOptions));
    }

    /// <summary>Decodes and validates an envelope.</summary>
    /// <param name="value">The provider-safe text.</param>
    /// <returns>The envelope.</returns>
    /// <example><code>var envelope = ContentTransformEnvelopeCodec.Decode(value);</code></example>
    public static ContentTransformEnvelope Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new FormatException("Content transform envelope format is not supported.");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<EnvelopeDto>(
                Base64UrlHelper.Decode(value[Prefix.Length..]),
                JsonOptions) ?? throw new FormatException("Content transform envelope is empty.");
            var envelope = FromDto(dto);
            Validate(envelope);
            return envelope;
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            throw new FormatException("Content transform envelope is invalid.", exception);
        }
    }

    private static void Validate(ContentTransformEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Version != 1)
        {
            throw new FormatException($"Content transform envelope version '{envelope.Version}' is not supported.");
        }

        if (envelope.LogicalLength < 0 || envelope.StoredLength < 0)
        {
            throw new FormatException("Content transform lengths must not be negative.");
        }

        var duplicate = envelope.Transforms
            .GroupBy(transform => transform?.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicate is not null)
        {
            throw new FormatException("Content transform identifiers must be non-empty and unique.");
        }
    }

    private static EnvelopeDto ToDto(ContentTransformEnvelope envelope) => new(
        envelope.Version,
        envelope.LogicalLength,
        envelope.LogicalContentHash,
        envelope.StoredLength,
        envelope.StoredContentHash,
        envelope.Transforms.Select(transform => new TransformDto(
            transform.Id,
            (transform.Properties ?? new PropertyBag()).ToDictionary(
                property => property.Key,
                property => PropertyBagScalarCodec.Encode(property.Value),
                StringComparer.Ordinal))).ToArray());

    private static ContentTransformEnvelope FromDto(EnvelopeDto dto) => new()
    {
        Version = dto.Version,
        LogicalLength = dto.LogicalLength,
        LogicalContentHash = dto.LogicalContentHash,
        StoredLength = dto.StoredLength,
        StoredContentHash = dto.StoredContentHash,
        Transforms = (dto.Transforms ?? []).Select(transform => new ContentTransformDescriptor
        {
            Id = transform.Id,
            Properties = new PropertyBag((transform.Properties ?? new Dictionary<string, string>())
                .ToDictionary(property => property.Key, property => PropertyBagScalarCodec.Decode(property.Value), StringComparer.Ordinal))
        }).ToArray()
    };

    private sealed record EnvelopeDto(
        int Version,
        long LogicalLength,
        string LogicalContentHash,
        long StoredLength,
        string StoredContentHash,
        IReadOnlyList<TransformDto> Transforms);

    private sealed record TransformDto(string Id, IReadOnlyDictionary<string, string> Properties);
}
