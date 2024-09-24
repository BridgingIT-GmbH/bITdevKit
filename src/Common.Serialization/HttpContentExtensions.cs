// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

public static class HttpContentExtensions
{
    public static async Task<T> ReadAsAsync<T>(this HttpContent content, CancellationToken cancellationToken = default)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream,
            DefaultSystemTextJsonSerializerOptions.Create(),
            cancellationToken);
    }

    public static async Task<T> ReadAsAsync<T>(
        this HttpContent content,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream,
            options ?? DefaultSystemTextJsonSerializerOptions.Create(),
            cancellationToken);
    }
}