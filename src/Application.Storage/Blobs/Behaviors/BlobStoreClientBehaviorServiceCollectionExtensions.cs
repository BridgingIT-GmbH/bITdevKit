// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides fluent registration helpers for blob-store client behaviors.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithLoggingBehavior()
///     .WithMetricsBehavior()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the logging blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage().WithLoggingBehavior();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithLoggingBehavior(this BlobStorageBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.WithBehavior((inner, serviceProvider, name) => new LoggingBlobStoreClientBehavior(
            serviceProvider.GetService<ILoggerFactory>(),
            inner,
            name));
    }

    /// <summary>
    /// Registers the metrics blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage().WithMetricsBehavior();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithMetricsBehavior(this BlobStorageBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.WithBehavior((inner, serviceProvider, name) => new MetricsBlobStoreClientBehavior(
            serviceProvider.GetService<IMeterFactory>(),
            inner,
            name));
    }

    /// <summary>
    /// Registers bounded, process-local upload concurrency admission for every named blob store.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional admission settings callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithUploadConcurrencyBehavior(options =>
    ///     {
    ///         options.MaxConcurrentUploads = 4;
    ///         options.MaxQueuedUploads = 16;
    ///     })
    ///     .WithInMemoryClient("reports");
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithUploadConcurrencyBehavior(
        this BlobStorageBuilderContext context,
        Action<UploadConcurrencyBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new UploadConcurrencyBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);
        var validation = options.Validate();
        if (validation.IsFailure)
        {
            throw new InvalidOperationException(
                validation.Errors.FirstOrDefault()?.Message ??
                "Blob upload concurrency options are invalid.");
        }

        if (context.Services.Any(descriptor =>
            descriptor.ServiceType == typeof(UploadConcurrencyBehaviorRegistration)))
        {
            throw new InvalidOperationException(
                "Blob-store upload concurrency behavior is already registered.");
        }

        context.Services.AddSingleton<UploadConcurrencyBehaviorRegistration>();
        context.Services.TryAddSingleton<BlobUploadAdmissionCoordinator>();
        context.Services.TryAddSingleton<IBlobUploadAdmissionCoordinator>(serviceProvider =>
            serviceProvider.GetRequiredService<BlobUploadAdmissionCoordinator>());

        return context.WithBehavior(
            (inner, serviceProvider, name) => new UploadConcurrencyBlobStoreClientBehavior(
                inner,
                serviceProvider.GetRequiredService<IBlobUploadAdmissionCoordinator>(),
                options,
                serviceProvider.GetService<ILoggerFactory>(),
                name));
    }

    private sealed class UploadConcurrencyBehaviorRegistration;

    /// <summary>
    /// Registers the exact-key download cache blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional cache behavior options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithCacheBehavior(options => options.SlidingExpiration = TimeSpan.FromMinutes(10));
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithCacheBehavior(
        this BlobStorageBuilderContext context,
        Action<CacheBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new CacheBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, serviceProvider, name) => new CacheBlobStoreClientBehavior(
            serviceProvider.GetService<ILoggerFactory>(),
            inner,
            serviceProvider.GetRequiredService<ICacheProvider>(),
            options,
            name));
    }

    /// <summary>
    /// Registers the extension-based content-type detection blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional content-type detection options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithContentTypeDetectionBehavior();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithContentTypeDetectionBehavior(
        this BlobStorageBuilderContext context,
        Action<ContentTypeDetectionBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new ContentTypeDetectionBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, _, name) => new ContentTypeDetectionBlobStoreClientBehavior(inner, options, name));
    }

    /// <summary>
    /// Registers the checksum verification blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional checksum verification options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithChecksumVerificationBehavior();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithChecksumVerificationBehavior(
        this BlobStorageBuilderContext context,
        Action<ChecksumVerificationBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new ChecksumVerificationBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, _, name) => new ChecksumVerificationBlobStoreClientBehavior(inner, options, name));
    }

    /// <summary>
    /// Registers the chaos blob-store client behavior for upload and download resilience testing.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional chaos options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithChaosBehavior(options => options.FailDownloadsEvery = 3);
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithChaosBehavior(
        this BlobStorageBuilderContext context,
        Action<ChaosBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new ChaosBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, _, name) => new ChaosBlobStoreClientBehavior(inner, options, name));
    }

    /// <summary>
    /// Registers the compression blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional compression options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithCompressionBehavior();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithCompressionBehavior(
        this BlobStorageBuilderContext context,
        Action<CompressionBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new CompressionBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, _, name) => new CompressionBlobStoreClientBehavior(inner, options, name));
    }

    /// <summary>
    /// Registers the encryption blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional encryption options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithEncryptionBehavior();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithEncryptionBehavior(
        this BlobStorageBuilderContext context,
        Action<EncryptionBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new EncryptionBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, serviceProvider, name) => new EncryptionBlobStoreClientBehavior(
            inner,
            serviceProvider.GetRequiredService<IEncryptionKeyProvider>(),
            options,
            name));
    }

    /// <summary>
    /// Registers the retry blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional retry options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithRetryBehavior(options => options.Attempts = 3);
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithRetryBehavior(
        this BlobStorageBuilderContext context,
        Action<RetryBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new RetryBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, serviceProvider, name) => new RetryBlobStoreClientBehavior(
            inner,
            options,
            name,
            serviceProvider.GetService<TimeProvider>()));
    }

    /// <summary>
    /// Registers the timeout blob-store client behavior.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional timeout options callback.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithTimeoutBehavior(options => options.Timeout = TimeSpan.FromSeconds(5));
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithTimeoutBehavior(
        this BlobStorageBuilderContext context,
        Action<TimeoutBlobStoreClientBehaviorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new TimeoutBlobStoreClientBehaviorOptions();
        configure?.Invoke(options);

        return context.WithBehavior((inner, serviceProvider, name) => new TimeoutBlobStoreClientBehavior(
            inner,
            options,
            name,
            serviceProvider.GetService<TimeProvider>()));
    }
}
