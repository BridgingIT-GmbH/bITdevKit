// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

/// <summary>Declares one NDJSON contract for execution and OpenAPI.</summary>
/// <example><code>app.MapGet("/stream", Handler).WithNdjson&lt;Weather&gt;(o => o.Mode = NdjsonMode.Envelope);</code></example>
public static class NdjsonEndpointConventionBuilderExtensions
{
    /// <summary>Captures endpoint overrides and matching stream metadata.</summary>
    /// <example><code>endpoint.WithNdjson&lt;Weather&gt;(o => o.MaxRecordBytes = 65536);</code></example>
    public static RouteHandlerBuilder WithNdjson<T>(this RouteHandlerBuilder builder, Action<NdjsonEndpointOptions> configure = null)
    {
        var overrides = new NdjsonEndpointOptions();
        configure?.Invoke(overrides);
        var captured = overrides.Capture();
        builder.Add(endpoint =>
        {
            var existing = endpoint.Metadata.OfType<NdjsonEndpointDescriptor>().LastOrDefault();
            if (existing is not null && (existing.RecordType != typeof(T) || (existing.IsExplicit && captured.Mode.HasValue && captured.Mode.Value != existing.Mode)))
            {
                throw new InvalidOperationException("Contradictory NDJSON endpoint declarations.");
            }

            var defaults = endpoint.ApplicationServices.GetService<IOptions<NdjsonStreamingOptions>>()?.Value ?? new NdjsonStreamingOptions();
            var json = endpoint.ApplicationServices.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
            var settings = (existing?.Settings ?? defaults.Capture(json)).Apply(captured);
            endpoint.Metadata.Add(new NdjsonEndpointDescriptor(typeof(T), settings));
        });
        builder.Produces<string>(StatusCodes.Status200OK, NdjsonDefaults.CONTENT_TYPE);
        builder.AddOpenApiOperationTransformer((operation, context, token) => new NdjsonOpenApiOperationTransformer().TransformAsync(operation, context, token));
        return builder;
    }

    /// <summary>Describes the matching declared mode, defaulting to inherited configuration.</summary>
    /// <example><code>endpoint.ProducesNdjson&lt;Weather&gt;();</code></example>
    public static RouteHandlerBuilder ProducesNdjson<T>(this RouteHandlerBuilder builder) => builder.WithNdjson<T>();

    /// <summary>Declares an explicit wire mode and matching per-record metadata.</summary>
    /// <example><code>endpoint.ProducesNdjson&lt;Weather&gt;(NdjsonMode.Envelope);</code></example>
    public static RouteHandlerBuilder ProducesNdjson<T>(this RouteHandlerBuilder builder, NdjsonMode mode) => builder.WithNdjson<T>(o => o.Mode = mode);
}
