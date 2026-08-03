// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using System.Diagnostics.Metrics;

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
                serviceProvider.GetService<IMeterFactory>(),
                inner));
    }
}
