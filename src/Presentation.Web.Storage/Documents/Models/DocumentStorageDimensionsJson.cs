// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using Application.Storage;
using Common;
using System.Text.Json;

/// <summary>Parses explicitly typed document Dimensions independently of descriptive Properties.</summary>
/// <example><code>var json = DocumentStorageDimensionsJson.Write(dimensions);</code></example>
public static class DocumentStorageDimensionsJson
{
    /// <summary>Parses optional typed collection JSON. Omitted or null collections preserve existing Dimensions.</summary>
    /// <param name="json">The typed named-object representation.</param>
    /// <returns>A validated immutable collection or a transport validation failure.</returns>
    /// <example><code>var result = DocumentStorageDimensionsJson.Parse(json);</code></example>
    public static Result<DocumentDimensions> Parse(string json)
    {
        if (json is null) return Result<DocumentDimensions>.Success(null);
        try { return Result<DocumentDimensions>.Success(JsonSerializer.Deserialize<DocumentDimensions>(json)); }
        catch (JsonException) { return Result<DocumentDimensions>.Failure(new DocumentStoreInvalidQueryError("Dimensions JSON is invalid.")); }
    }

    /// <summary>Writes a collection as a named object of typed scalar envelopes.</summary>
    /// <param name="dimensions">The collection to encode.</param>
    /// <returns>The exact transport JSON representation.</returns>
    /// <example><code>var result = DocumentStorageDimensionsJson.Write(dimensions);</code></example>
    public static string Write(DocumentDimensions dimensions) => JsonSerializer.Serialize(dimensions ?? DocumentDimensions.Empty);

    /// <summary>Parses optional bounded all/any filter JSON with typed operands.</summary>
    /// <param name="json">The filter representation.</param>
    /// <returns>The feature-owned filter or a validation failure.</returns>
    /// <example><code>var result = DocumentStorageDimensionsJson.ParseFilter(filterJson);</code></example>
    public static Result<DocumentDimensionFilter> ParseFilter(string json)
    {
        if (json is null) return Result<DocumentDimensionFilter>.Success(null);
        try { return Result<DocumentDimensionFilter>.Success(JsonSerializer.Deserialize<DocumentDimensionFilter>(json)); }
        catch (JsonException) { return Result<DocumentDimensionFilter>.Failure(new DocumentStoreInvalidQueryError("Dimension filter JSON is invalid.")); }
    }
}
