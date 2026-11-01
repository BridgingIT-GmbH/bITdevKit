// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers validated shared NDJSON defaults without global observers.</summary>
/// <example><code>services.AddNdjsonStreaming(o => o.MaxRecordBytes = 65536);</code></example>
public static class NdjsonServiceCollectionExtensions
{
    /// <summary>Registers startup-validated options and a clock fallback.</summary>
    /// <example><code>services.AddNdjsonStreaming();</code></example>
    public static IServiceCollection AddNdjsonStreaming(this IServiceCollection services, Action<NdjsonStreamingOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.ConfigureAll<Microsoft.AspNetCore.OpenApi.OpenApiOptions>(o => o.AddOperationTransformer<NdjsonOpenApiOperationTransformer>());
        var options = services.AddOptions<NdjsonStreamingOptions>();
        if (configure is not null) { options.Configure(configure); }

        options.Validate(value => { value.Validate(); return true; }).ValidateOnStart();
        return services;
    }
}
