// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides fluent registration helpers for document-store client behaviors.
/// </summary>
/// <example>
/// <code>
/// services.AddDocumentStorage()
///     .WithMetricsBehavior&lt;Person&gt;()
///     .WithClient&lt;Person&gt;(sp => new DocumentStoreClient&lt;Person&gt;(
///         new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;())));
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>Profiles calls for one document type with named-client and document-type dimensions.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="context">The named document-storage builder.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>services.AddDocumentStorage().WithProfilingBehavior&lt;Person&gt;();</code></example>
    public static DocumentStorageBuilderContext WithProfilingBehavior<T>(this DocumentStorageBuilderContext context) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.WithBehavior<T, ProfilingDocumentStoreClientBehavior<T>>((inner, services) => new(inner, services.GetService<IOperationProfiler>()));
    }

    /// <summary>Profiles an existing directly registered document client using the optional operation profiler.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="context">The direct document-client builder.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>builder.WithProfilingBehavior();</code></example>
    public static DocumentStoreBuilderContext<T> WithProfilingBehavior<T>(this DocumentStoreBuilderContext<T> context) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.WithBehavior<ProfilingDocumentStoreClientBehavior<T>>((inner, services) => new(inner, services.GetService<IOperationProfiler>()));
    }

    /// <summary>
    /// Registers the metrics document-store client behavior for one document type.
    /// </summary>
    /// <typeparam name="T">The document type handled by the decorated client.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithMetricsBehavior&lt;Person&gt;();
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithMetricsBehavior<T>(this DocumentStorageBuilderContext context)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.WithBehavior<T, MetricsDocumentStoreClientBehavior<T>>(
            (inner, serviceProvider) => new MetricsDocumentStoreClientBehavior<T>(
                inner,
                serviceProvider.GetService<IMetricsService>()));
    }
}
