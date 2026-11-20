// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using Application.Storage;
using Common;
using System.Text.Json;

/// <summary>Parses explicitly typed blob Dimensions independently of descriptive Properties.</summary>
/// <example><code>var json = BlobStorageDimensionsJson.Write(dimensions);</code></example>
public static class BlobStorageDimensionsJson
{
    /// <summary>Parses optional typed collection JSON. Omitted or null collections preserve existing Dimensions.</summary>
    /// <param name="json">The typed named-object representation.</param>
    /// <returns>A validated immutable collection or a transport validation failure.</returns>
    /// <example><code>var result = BlobStorageDimensionsJson.Parse(json);</code></example>
    public static Result<BlobDimensions> Parse(string json)
    {
        if (json is null) return Result<BlobDimensions>.Success(null);
        try { return Result<BlobDimensions>.Success(JsonSerializer.Deserialize<BlobDimensions>(json)); }
        catch (JsonException) { return Result<BlobDimensions>.Failure(new BlobStoreValidationError("Dimensions JSON is invalid.")); }
    }

    /// <summary>Writes a collection as a named object of typed scalar envelopes.</summary>
    /// <param name="dimensions">The collection to encode.</param>
    /// <returns>The exact transport JSON representation.</returns>
    /// <example><code>var result = BlobStorageDimensionsJson.Write(dimensions);</code></example>
    public static string Write(BlobDimensions dimensions) => JsonSerializer.Serialize(dimensions ?? BlobDimensions.Empty);

    /// <summary>Parses optional bounded all/any filter JSON with typed operands.</summary>
    /// <param name="json">The filter representation.</param>
    /// <returns>The feature-owned filter or a validation failure.</returns>
    /// <example><code>var result = BlobStorageDimensionsJson.ParseFilter(filterJson);</code></example>
    public static Result<BlobDimensionFilter> ParseFilter(string json)
    {
        if (json is null) return Result<BlobDimensionFilter>.Success(null);
        try { return Result<BlobDimensionFilter>.Success(JsonSerializer.Deserialize<BlobDimensionFilter>(json)); }
        catch (JsonException) { return Result<BlobDimensionFilter>.Failure(new BlobStoreValidationError("Dimension filter JSON is invalid.")); }
    }
}
