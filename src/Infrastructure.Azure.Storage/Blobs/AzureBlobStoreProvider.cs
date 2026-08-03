// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using global::Azure;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Implements the provider-neutral blob-store contract using Azure Blob Storage.
/// </summary>
/// <example>
/// <code>
/// var provider = new AzureBlobStoreProvider(blobServiceClient);
/// var result = await provider.ExistsAsync(new BlobKey("reports", "2026/06/report.pdf"));
/// </code>
/// </example>
public partial class AzureBlobStoreProvider : IBlobStoreProvider, IBlobStoreContainerCatalog
{
    /// <summary>
    /// Defines the provider name used for diagnostics and continuation-token binding.
    /// </summary>
    public const string ProviderName = "Azure Blob Storage";

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
    public async Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(upload, this.options);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        var metadataResult = ToMetadata(upload.Properties);
        if (metadataResult.IsFailure)
        {
            return Result<BlobInfo>.Failure(metadataResult);
        }

        ApplyExpiration(metadataResult.Value, upload.ExpiresAt);

        try
        {
            await this.backend.UploadAsync(
                    upload.Key,
                    upload.Content,
                    new AzureBlobUploadRequest(
                        upload.ContentType?.MimeType(),
                        metadataResult.Value,
                        ToTags(upload.ExpiresAt),
                        upload.OverwriteMode == BlobOverwriteMode.FailIfExists,
                        upload.ExpectedContentHash,
                        this.options.MaxBlobSize,
                        ContentHashMetadataKey),
                    cancellationToken)
                .ConfigureAwait(false);

            var properties = await this.backend.GetPropertiesAsync(upload.Key, cancellationToken).ConfigureAwait(false);
            return Result<BlobInfo>.Success(ToInfo(properties));
        }
        catch (AzureBlobStoreBackendException exception)
        {
            return Result<BlobInfo>.Failure(exception.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobInfo>.Failure(MapException(exception, upload.Key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<BlobDownload>.Failure(validation);
        }

        try
        {
            var properties = await this.backend.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
            var content = await this.backend.OpenReadAsync(key, cancellationToken).ConfigureAwait(false);

            return Result<BlobDownload>.Success(new BlobDownload
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
            return Result<BlobDownload>.Failure(MapException(exception, key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> GetPropertiesAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        try
        {
            return Result<BlobInfo>.Success(ToInfo(await this.backend.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobInfo>.Failure(MapException(exception, key));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UpdatePropertiesAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(update);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        var metadataResult = ToMetadata(update.Properties);
        if (metadataResult.IsFailure)
        {
            return Result<BlobInfo>.Failure(metadataResult);
        }

        ApplyExpiration(metadataResult.Value, update.ExpiresAt);

        try
        {
            var current = await this.backend.GetPropertiesAsync(update.Key, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(current.Metadata?.GetValueOrDefault(ContentHashMetadataKey)))
            {
                metadataResult.Value[ContentHashMetadataKey] = current.Metadata[ContentHashMetadataKey];
            }

            var updated = await this.backend.UpdatePropertiesAsync(
                    update.Key,
                    update.ContentType?.MimeType(),
                    metadataResult.Value,
                    ToTags(update.ExpiresAt),
                    update.IfMatchETag,
                    cancellationToken)
                .ConfigureAwait(false);

            return Result<BlobInfo>.Success(ToInfo(updated));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobInfo>.Failure(MapException(exception, update.Key));
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
    public async Task<Result<BlobPage>> ListPageAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobQueryValidator.NormalizeAndValidate(
            ProviderName,
            query,
            this.options,
            this.Capabilities,
            this.continuationTokenProtector);
        if (validation.IsFailure)
        {
            return Result<BlobPage>.Failure(validation);
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

            return Result<BlobPage>.Success(new BlobPage
            {
                Items = page.Items.Select(ToInfo).ToList(),
                ContinuationToken = CreateContinuationToken(validation.Value.QueryHash, page.ContinuationToken)
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobPage>.Failure(MapException(exception));
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

    private static BlobInfo ToInfo(AzureBlobProperties properties) => new()
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
        Properties = ToPropertyBag(properties.Metadata)
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

    private static Result<Dictionary<string, string>> ToMetadata(PropertyBag properties)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in properties.SafeNull())
        {
            if (string.Equals(key, ContentHashMetadataKey, StringComparison.OrdinalIgnoreCase))
            {
                return Result<Dictionary<string, string>>.Failure(new BlobStoreSerializationError(
                    $"Azure Blob metadata key '{key}' is reserved."));
            }

            if (string.Equals(key, ExpiresAtMetadataKey, StringComparison.OrdinalIgnoreCase))
            {
                return Result<Dictionary<string, string>>.Failure(new BlobStoreSerializationError(
                    $"Azure Blob metadata key '{key}' is reserved."));
            }

            if (!IsValidMetadataKey(key))
            {
                return Result<Dictionary<string, string>>.Failure(new BlobStoreSerializationError(
                    $"Azure Blob metadata key '{key}' is invalid."));
            }

            var valueResult = ToMetadataValue(key, value);
            if (valueResult.IsFailure)
            {
                return Result<Dictionary<string, string>>.Failure(valueResult);
            }

            metadata[key] = valueResult.Value;
        }

        return Result<Dictionary<string, string>>.Success(metadata);
    }

    private static PropertyBag ToPropertyBag(IDictionary<string, string> metadata)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata.SafeNull())
        {
            if (string.Equals(key, ContentHashMetadataKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(key, ExpiresAtMetadataKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values[key] = FromMetadataValue(value);
        }

        return new PropertyBag(values);
    }

    private static Result<string> ToMetadataValue(string key, object value)
    {
        try
        {
            return Result<string>.Success(PropertyBagScalarCodec.Encode(value));
        }
        catch (ArgumentException exception)
        {
            return Result<string>.Failure(new BlobStoreSerializationError(
                $"Azure Blob metadata value for key '{key}' is not supported: {exception.Message}"));
        }
    }

    private static object FromMetadataValue(string value)
    {
        try
        {
            return PropertyBagScalarCodec.Decode(value);
        }
        catch (FormatException exception)
        {
            throw new AzureBlobStoreBackendException(new BlobStoreSerializationError(
                $"Azure Blob metadata contains an invalid typed value: {exception.Message}"));
        }
    }

    private static bool IsValidMetadataKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && MetadataKeyRegex().IsMatch(key);

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
