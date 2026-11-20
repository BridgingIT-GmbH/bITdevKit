// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Globalization;
using System.Text.RegularExpressions;
using global::Azure;

/// <summary>
/// Implements the provider-neutral blob-store contract using Azure Blob Storage.
/// </summary>
/// <example>
/// <code>
/// var provider = new AzureBlobStoreProvider(blobServiceClient);
/// var result = await provider.ExistsAsync(new BlobKey("reports", "2026/06/report.pdf"));
/// </code>
/// </example>
public partial class AzureBlobStoreProvider : IBlobStoreProvider, IBlobStoreRetentionProvider, IBlobStoreContainerCatalog
{
    /// <summary>
    /// Defines the provider name used for diagnostics and continuation-token binding.
    /// </summary>
    public const string ProviderName = "Azure Blob Storage";

    private const string PropertiesMetadataKey = "bdk_properties";
    private const string TransformsMetadataKey = "bdk_transforms";
    private const string ContentHashMetadataKey = "bdk_contenthash";
    private const string ExpiresAtMetadataKey = "bdk_expiresat";
    private const string ExpiresAtTagKey = "bdk_expiresat";
    private const string ExpiresAtFormat = "yyyyMMddHHmmssfffffff";
    private readonly IAzureBlobStoreBackend backend;
    private readonly BlobStoreOptions options;
    private readonly IContinuationTokenProtector continuationTokenProtector;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Result<IReadOnlyList<string>>.Success(
                await this.backend.ListContainersAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<string>>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobStoreProvider" /> class.
    /// </summary>
    /// <param name="serviceClient">The Azure Blob service client.</param>
    /// <param name="options">The optional blob-store options.</param>
    /// <param name="continuationTokenProtector">The optional continuation-token protector.</param>
    /// <example>
    /// <code>
    /// var provider = new AzureBlobStoreProvider(blobServiceClient, new BlobStoreOptions());
    /// </code>
    /// </example>
    public AzureBlobStoreProvider(
        BlobServiceClient serviceClient,
        BlobStoreOptions options = null,
        IContinuationTokenProtector continuationTokenProtector = null)
        : this(
            new AzureBlobStoreBackend(serviceClient ?? throw new ArgumentNullException(nameof(serviceClient))),
            options,
            continuationTokenProtector)
    {
    }

    /// <summary>
    /// Initializes a derived provider with a custom Azure transport backend.
    /// </summary>
    /// <param name="backend">The Azure transport backend.</param>
    /// <param name="options">The optional blob-store options.</param>
    /// <param name="continuationTokenProtector">The optional continuation-token protector.</param>
    /// <example>
    /// <code>
    /// protected TestProvider(IAzureBlobStoreBackend backend) : base(backend) { }
    /// </code>
    /// </example>
    protected AzureBlobStoreProvider(
        IAzureBlobStoreBackend backend,
        BlobStoreOptions options = null,
        IContinuationTokenProtector continuationTokenProtector = null)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.options = options ?? new BlobStoreOptions();
        this.continuationTokenProtector = continuationTokenProtector;
    }

    /// <inheritdoc />
    public BlobStoreProviderCapabilities Capabilities { get; } = CreateCapabilities();

    /// <summary>
    /// Creates the provider capabilities for Azure Blob Storage.
    /// </summary>
    /// <returns>The provider capability descriptor.</returns>
    /// <example>
    /// <code>
    /// var capabilities = AzureBlobStoreProvider.CreateCapabilities();
    /// </code>
    /// </example>
    public static BlobStoreProviderCapabilities CreateCapabilities() => new()
    {
        SupportsContinuationPaging = true,
        SupportsPrefixListing = true,
        SupportsFullContainerScan = true,
        SupportsProperties = true,
        SupportsContentType = true,
        SupportsETag = true,
        SupportsContentHash = true,
        SupportsInternalLeases = false,
        SupportsConditionalPropertiesUpdate = true,
        SupportsStreamingUpload = true,
        SupportsStreamingDownload = true,
        SupportsExpiration = true,
        SupportsRetentionSweep = true,
        SupportsNativeRetention = true
    };

    /// <inheritdoc />
    public async Task<Result<StoredBlobInfo>> UploadAsync(
        StoredBlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (upload?.Dimensions is not null) return Result<StoredBlobInfo>.Failure(new BlobDimensionsNotSupportedError());

        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(upload, this.options);
        if (validation.IsFailure)
        {
            return Result<StoredBlobInfo>.Failure(validation);
        }

        var metadataResult = ToMetadata(upload.Properties, upload.TransformMetadata);
        if (metadataResult.IsFailure)
        {
            return Result<StoredBlobInfo>.Failure(metadataResult);
        }

        ApplyExpiration(metadataResult.Value, upload.ExpiresAt);

        try
        {
            var committed = await this.backend.UploadAsync(
                    upload.Key,
                    upload.Content,
                    new AzureBlobUploadRequest(
                        upload.ContentType?.MimeType(),
                        metadataResult.Value,
                        ToTags(upload.ExpiresAt),
                        upload.OverwriteMode == BlobOverwriteMode.FailIfExists,
                        upload.ExpectedContentHash,
                        this.options.MaxBlobSize,
                        ContentHashMetadataKey,
                        upload.IfMatchETag, upload.Properties is null ? PropertiesMetadataKey : null),
                    cancellationToken)
                .ConfigureAwait(false);

            var properties = committed.CommittedProperties
                ?? await this.backend.GetPropertiesAsync(upload.Key, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(properties.ETag, committed.ETag, StringComparison.Ordinal))
            {
                return Result<StoredBlobInfo>.Failure(new BlobStorePartialUpdateError(
                    "The Azure backend did not return properties for the committed upload version.",
                    "The upload was committed, but its properties are unavailable.",
                    "A later version cannot be used to describe the committed upload."));
            }

            return Result<StoredBlobInfo>.Success(ToInfo(properties));
        }
        catch (AzureBlobStoreBackendException exception)
        {
            return Result<StoredBlobInfo>.Failure(exception.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException exception) when (exception.Status == 404 && !string.IsNullOrWhiteSpace(upload.IfMatchETag))
        {
            return Result<StoredBlobInfo>.Failure(new BlobStoreConflictError("The conditional upload target no longer exists."));
        }
        catch (Exception exception)
        {
            return Result<StoredBlobInfo>.Failure(MapException(exception, upload.Key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<StoredBlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<StoredBlobDownload>.Failure(validation);
        }

        try
        {
            var properties = await this.backend.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
            var content = await this.backend.OpenReadAsync(key, cancellationToken).ConfigureAwait(false);

            return Result<StoredBlobDownload>.Success(new StoredBlobDownload
            {
                Info = ToInfo(properties),
                Content = content
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<StoredBlobDownload>.Failure(MapException(exception, key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<StoredBlobInfo>> GetPropertiesAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<StoredBlobInfo>.Failure(validation);
        }

        try
        {
            return Result<StoredBlobInfo>.Success(ToInfo(await this.backend.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<StoredBlobInfo>.Failure(MapException(exception, key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<StoredBlobInfo>> UpdatePropertiesAsync(
        StoredBlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(update);
        if (validation.IsFailure)
        {
            return Result<StoredBlobInfo>.Failure(validation);
        }

        try
        {
            var current = await this.backend.GetPropertiesAsync(update.Key, cancellationToken).ConfigureAwait(false);
            var currentInfo = ToInfo(current);
            var metadataResult = ToMetadata(update.Properties ?? currentInfo.Properties,
                update.TransformMetadata ?? currentInfo.TransformMetadata);
            if (metadataResult.IsFailure)
            {
                return Result<StoredBlobInfo>.Failure(metadataResult);
            }

            ApplyExpiration(metadataResult.Value, update.ExpiresAt);
            if (!string.IsNullOrWhiteSpace(current.Metadata?.GetValueOrDefault(ContentHashMetadataKey)))
            {
                metadataResult.Value[ContentHashMetadataKey] = current.Metadata[ContentHashMetadataKey];
            }

            var updated = await this.backend.UpdatePropertiesAsync(
                    update.Key,
                    update.ContentType?.MimeType(),
                    metadataResult.Value,
                    ToTags(update.ExpiresAt),
                    update.IfMatchETag ?? current.ETag,
                    cancellationToken)
                .ConfigureAwait(false);

            return Result<StoredBlobInfo>.Success(ToInfo(updated));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<StoredBlobInfo>.Failure(MapException(exception, update.Key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<bool>.Failure((IResult)validation);
        }

        try
        {
            return Result<bool>.Success(await this.backend.ExistsAsync(key, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsStatus(exception, 404))
        {
            return Result<bool>.Success(false);
        }
        catch (Exception exception)
        {
            return Result<bool>.Failure(MapException(exception, key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<StoredBlobPage>> ListPageAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query?.DimensionFilter is not null) return Result<StoredBlobPage>.Failure(new BlobDimensionsNotSupportedError());

        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobQueryValidator.NormalizeAndValidate(
            ProviderName,
            query,
            this.options,
            this.Capabilities,
            this.continuationTokenProtector);
        if (validation.IsFailure)
        {
            return Result<StoredBlobPage>.Failure(validation);
        }

        try
        {
            var nativeToken = validation.Value.ContinuationToken?.NativeToken;
            var page = await this.backend.ListPageAsync(
                    validation.Value.Query.Container,
                    validation.Value.Query.Prefix,
                    nativeToken,
                    validation.Value.Take,
                    cancellationToken)
                .ConfigureAwait(false);

            return Result<StoredBlobPage>.Success(new StoredBlobPage
            {
                Items = page.Items.Select(ToInfo).ToList(),
                ContinuationToken = this.CreateContinuationToken(validation.Value.QueryHash, page.ContinuationToken)
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<StoredBlobPage>.Failure(MapException(exception));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return validation;
        }

        validation = BlobValidator.Validate(options);
        if (validation.IsFailure)
        {
            return validation;
        }

        try
        {
            await this.backend.DeleteIfExistsAsync(key, options?.IfMatchETag, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsStatus(exception, 404))
        {
            return Result.Success();
        }
        catch (Exception exception)
        {
            return Result.Failure(MapException(exception, key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobRetentionSweepResult>> SweepExpiredAsync(
        BlobRetentionSweepRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var deleted = 0;
        var deletedKeys = new List<BlobKey>();
        var batches = 0;
        var continuationToken = default(string);

        try
        {
            for (var batch = 0; batch < Math.Max(1, request.MaxBatches); batch++)
            {
                var page = await this.backend
                    .ListExpiredAsync(FormatExpiresAt(request.ExpiresOnOrBefore), continuationToken, Math.Max(1, request.BatchSize), cancellationToken)
                    .ConfigureAwait(false);

                if (page.Items.Count == 0)
                {
                    break;
                }

                batches++;
                foreach (var key in page.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.backend.DeleteIfExistsAsync(key, null, cancellationToken).ConfigureAwait(false);
                    deleted++;
                    deletedKeys.Add(key);
                }

                continuationToken = page.ContinuationToken;
                if (string.IsNullOrWhiteSpace(continuationToken))
                {
                    break;
                }

                if (request.BatchDelay > TimeSpan.Zero)
                {
                    await Task.Delay(request.BatchDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return Result<BlobRetentionSweepResult>.Success(new BlobRetentionSweepResult
            {
                StoreName = request.StoreName,
                ProviderName = request.ProviderName,
                BatchCount = batches,
                DeletedCount = deleted,
                DeletedKeys = deletedKeys,
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobRetentionSweepResult>.Failure(MapException(exception));
        }
    }

    private static StoredBlobInfo ToInfo(AzureBlobProperties properties) => new()
    {
        Key = properties.Key,
        Length = properties.Length,
        ContentType = string.IsNullOrWhiteSpace(properties.HttpHeaders?.ContentType)
            ? null
            : ContentTypeExtensions.FromMimeType(properties.HttpHeaders.ContentType),
        ContentHash = properties.Metadata?.GetValueOrDefault(ContentHashMetadataKey),
        ETag = properties.ETag,
        CreatedAt = properties.CreatedAt,
        LastModifiedAt = properties.LastModifiedAt,
        ExpiresAt = ToUtc(ParseExpiresAt(properties.Metadata?.GetValueOrDefault(ExpiresAtMetadataKey))),
        Properties = ReadBag(properties.Metadata, PropertiesMetadataKey),
        TransformMetadata = ReadBag(properties.Metadata, TransformsMetadataKey)
    };

    private static void ApplyExpiration(IDictionary<string, string> metadata, DateTimeOffset? expiresAt)
    {
        if (expiresAt is null)
        {
            metadata.Remove(ExpiresAtMetadataKey);
            return;
        }

        metadata[ExpiresAtMetadataKey] = FormatExpiresAt(expiresAt.Value);
    }

    private static IDictionary<string, string> ToTags(DateTimeOffset? expiresAt) =>
        expiresAt is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ExpiresAtTagKey] = FormatExpiresAt(expiresAt.Value)
            };

    private static string FormatExpiresAt(DateTimeOffset expiresAt) =>
        expiresAt.UtcDateTime.ToString(ExpiresAtFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset? ToUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static DateTimeOffset? ParseExpiresAt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParseExact(
            value,
            ExpiresAtFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt)
                ? expiresAt
                : null;
    }

    private static Result<Dictionary<string, string>> ToMetadata(PropertyBag properties, PropertyBag transforms)
    {
        try
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            WriteBag(metadata, PropertiesMetadataKey, properties);
            WriteBag(metadata, TransformsMetadataKey, transforms);
            var bytes = metadata.Sum(item => Encoding.UTF8.GetByteCount(item.Key) + Encoding.UTF8.GetByteCount(item.Value));
            return bytes > 8192
                ? Result<Dictionary<string, string>>.Failure(new BlobStoreSizeLimitExceededError(bytes, 8192))
                : Result<Dictionary<string, string>>.Success(metadata);
        }
        catch (ArgumentException exception)
        {
            return Result<Dictionary<string, string>>.Failure(new BlobStoreSerializationError(exception.Message));
        }
    }

    private static void WriteBag(IDictionary<string, string> metadata, string key, PropertyBag values)
    {
        var json = BlobPropertyValues.Serialize(values);
        if (json is not null)
        {
            metadata[key] = Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(json));
        }
    }

    private static PropertyBag ReadBag(IDictionary<string, string> metadata, string key) =>
        metadata?.TryGetValue(key, out var value) == true
            ? BlobPropertyValues.Deserialize(Encoding.UTF8.GetString(Base64UrlHelper.Decode(value)))
            : new PropertyBag();

    private string CreateContinuationToken(string queryHash, string nativeToken)
    {
        if (string.IsNullOrWhiteSpace(nativeToken))
        {
            return null;
        }

        var result = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
        {
            Provider = ProviderName,
            QueryHash = queryHash,
            NativeToken = nativeToken
        }, this.continuationTokenProtector);

        return result.IsSuccess ? result.Value : null;
    }

    private static IResultError MapException(Exception exception, BlobKey key = null)
    {
        if (exception is AzureBlobStoreBackendException backend)
        {
            return backend.Error;
        }

        if (exception is RequestFailedException request)
        {
            return request.Status switch
            {
                404 when key is not null => new BlobStoreNotFoundError(key),
                409 or 412 => new BlobStoreConflictError(request.Message),
                408 => new BlobStoreTimeoutError("azure-blob-request", TimeSpan.Zero),
                _ => new BlobStoreProviderError(request.GetFullMessage())
            };
        }

        return new BlobStoreProviderError(exception.GetFullMessage());
    }

    private static bool IsStatus(Exception exception, int status) =>
        exception is RequestFailedException request && request.Status == status;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MetadataKeyRegex();

}
