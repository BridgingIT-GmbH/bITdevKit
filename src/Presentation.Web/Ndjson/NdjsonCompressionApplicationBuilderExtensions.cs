// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>Integrates the host's single compression middleware with explicit NDJSON opt-in.</summary>
/// <example><code>app.UseRouting(); app.UseNdjsonResponseCompression();</code></example>
public static class NdjsonCompressionApplicationBuilderExtensions
{
    /// <summary>Bypasses automatic compression for NDJSON unless its descriptor opts in.</summary>
    /// <example><code>services.AddResponseCompression(); app.UseNdjsonResponseCompression();</code></example>
    public static IApplicationBuilder UseNdjsonResponseCompression(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseWhen(context =>
        {
            var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<NdjsonEndpointDescriptor>();
            return descriptor is null || descriptor.EnableCompression;
        }, branch => branch.UseResponseCompression());
    }
}
