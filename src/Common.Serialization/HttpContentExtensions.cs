// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
///     Provides JSON deserialization helpers for HTTP content.
/// </summary>
public static class HttpContentExtensions
{
    /// <summary>
    ///     Deserializes HTTP content using the DevKit default JSON options.
    /// </summary>
    /// <typeparam name="T">The value type to deserialize.</typeparam>
    /// <param name="content">The HTTP content to read.</param>
    /// <param name="cancellationToken">A token that can cancel reading or deserialization.</param>
    /// <returns>The deserialized value.</returns>
    public static async Task<T> ReadAsAsync<T>(this HttpContent content, CancellationToken cancellationToken = default)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<T>(stream,
            DefaultJsonSerializerOptions.Create(),
            cancellationToken);
    }

    /// <summary>
    ///     Deserializes HTTP content using the supplied JSON options, or the DevKit defaults when none are supplied.
    /// </summary>
    /// <typeparam name="T">The value type to deserialize.</typeparam>
    /// <param name="content">The HTTP content to read.</param>
    /// <param name="options">The JSON options to use, or <see langword="null"/> to use the DevKit defaults.</param>
    /// <param name="cancellationToken">A token that can cancel reading or deserialization.</param>
    /// <returns>The deserialized value.</returns>
    public static async Task<T> ReadAsAsync<T>(
        this HttpContent content,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<T>(stream,
            options ?? DefaultJsonSerializerOptions.Create(),
            cancellationToken);
    }
}
