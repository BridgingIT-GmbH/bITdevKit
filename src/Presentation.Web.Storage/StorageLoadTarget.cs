// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

internal sealed class StorageLoadTarget
{
    private const string OWNER_PROPERTY = "bdk_diagnostic_owner";
    private static readonly DocumentTypeIdentity DiagnosticType = new("bdk.storage.diagnostics.v1");
    private readonly IBlobStoreProvider blobs;
    private readonly IDocumentStoreProvider documents;
    private readonly IFileStorageProvider files;
    private readonly string owner;
    private readonly BlobKey blobKey;
    private readonly DocumentKey documentKey;
    private string etag;
    private string hash;
    private bool attempted;

    internal string Key { get; }

    internal bool PendingCleanup { get; private set; }

    internal StorageLoadTarget(IServiceProvider services, string feature, StorageLoadRequest request, string owner, int worker, bool cleanupOnly = false)
    {
        this.owner = owner;
        this.attempted = cleanupOnly;
        this.PendingCleanup = cleanupOnly;
        this.Key = $"bdk-diag-{owner}-{worker}.bin";
        this.blobKey = new(request.Container, this.Key);
        this.documentKey = new("bdk-diag-" + owner, worker.ToString(System.Globalization.CultureInfo.InvariantCulture));
        switch (feature)
        {
            case "blobs":
                var registration = services.GetServices<BlobStoreClientRegistration>().SingleOrDefault(x => x.Name == request.Target) ?? throw new StorageLoadValidationException("Select a registered blob client.");
                this.blobs = services.GetRequiredKeyedService<IBlobStoreProvider>(registration.Name);
                break;
            case "documents":
                var descriptor = services.GetServices<DocumentStoreClientDescriptor>().SingleOrDefault(x => x.ClientId == request.Target) ?? throw new StorageLoadValidationException("Select a registered document client.");
                this.documents = services.GetRequiredKeyedService<IDocumentStoreProvider>(new DocumentStoreServiceKey(descriptor.DocumentType, descriptor.Name));
                break;
            case "files":
                var factory = services.GetRequiredService<IFileStorageProviderFactory>();

                if (!factory.GetProviderNames().Contains(request.Target))
                {
                    throw new StorageLoadValidationException("Select a registered file provider.");
                }

                var fileProvider = factory.CreateProvider(request.Target);
                var seen = new HashSet<IFileStorageProvider>(ReferenceEqualityComparer.Instance);

                while (fileProvider is IFileStorageBehavior behavior)
                {
                    if (!seen.Add(fileProvider))
                    {
                        throw new StorageLoadValidationException("The configured file-provider behavior chain contains a cycle.");
                    }

                    fileProvider = behavior.InnerProvider;
                }

                this.files = fileProvider ?? throw new StorageLoadValidationException("The configured file provider is unavailable.");
                break;
            default:
                throw new StorageLoadValidationException("Unknown storage feature.");
        }
    }

    internal async Task SeedAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (this.blobs is not null)
        {
            if (this.blobs is not IBlobStoreContainerCatalog catalog)
            {
                throw new StorageLoadValidationException("This provider cannot verify an existing container for a temporary load test.");
            }

            var containers = await catalog.ListContainersAsync(cancellationToken).ConfigureAwait(false);
            Ensure(containers);

            if (!containers.Value.Contains(this.blobKey.Container))
            {
                throw new StorageLoadValidationException("Select an existing container. Load tests never create containers.");
            }
        }

        if (this.files is not null)
        {
            var exists = await this.files.FileExistsAsync(this.Key, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (exists.IsSuccess || !Missing(exists))
            {
                throw new StorageLoadException("The temporary file name is unavailable; no file was changed.");
            }
        }

        this.attempted = true;
        this.PendingCleanup = true;
        await this.WriteAsync(payload, createOnly: true, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<long> WriteAsync(byte[] payload, bool createOnly, CancellationToken cancellationToken)
    {
        if (this.blobs is not null)
        {
            using var content = new MemoryStream(payload, false);
            var result = await this.blobs.UploadAsync(new() { Key = this.blobKey, Content = content, Properties = new() { [OWNER_PROPERTY] = this.owner }, OverwriteMode = createOnly ? BlobOverwriteMode.FailIfExists : BlobOverwriteMode.Overwrite }, cancellationToken).ConfigureAwait(false);
            Ensure(result);
            this.etag = result.Value.ETag;
        }
        else if (this.documents is not null)
        {
            var result = await this.documents.UpsertAsync(DiagnosticType, new() { Key = this.documentKey, Content = payload, StoredContentHash = this.hash ??= "sha256:" + Convert.ToHexStringLower(SHA256.HashData(payload)), Properties = new() { [OWNER_PROPERTY] = this.owner }, Options = new() { CreateOnly = createOnly, IfMatchETag = createOnly ? null : this.etag } }, cancellationToken).ConfigureAwait(false);
            Ensure(result);
            this.etag = result.Value.ETag;
        }
        else
        {
            using var content = new MemoryStream(payload, false);
            Ensure(await this.files.WriteFileAsync(this.Key, content, cancellationToken: cancellationToken).ConfigureAwait(false));
        }

        return payload.Length;
    }

    internal async Task<long> ReadAsync(CancellationToken cancellationToken)
    {
        if (this.documents is not null)
        {
            var result = await this.documents.GetAsync(DiagnosticType, this.documentKey, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            Ensure(result);

            return result.Value.Content.LongLength;
        }

        Stream stream;

        if (this.blobs is not null)
        {
            var result = await this.blobs.DownloadAsync(this.blobKey, cancellationToken).ConfigureAwait(false);
            Ensure(result);
            stream = result.Value.Content;
        }
        else
        {
            var result = await this.files.ReadFileAsync(this.Key, cancellationToken: cancellationToken).ConfigureAwait(false);
            Ensure(result);
            stream = result.Value;
        }

        await using var content = stream;
        var buffer = ArrayPool<byte>.Shared.Rent(65536);

        try
        {
            long bytes = 0;
            int count;

            while ((count = await content.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                bytes += count;
            }

            return bytes;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal async Task CleanupAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (!this.attempted || !this.PendingCleanup)
        {
            return;
        }

        if (this.blobs is not null)
        {
            var current = await this.blobs.GetPropertiesAsync(this.blobKey, cancellationToken).ConfigureAwait(false);

            if (Missing(current))
            {
                this.PendingCleanup = false;

                return;
            }

            Ensure(current);
            EnsureOwner(current.Value.Properties);
            Ensure(await this.blobs.DeleteAsync(this.blobKey, new() { IfMatchETag = current.Value.ETag }, cancellationToken).ConfigureAwait(false));
            var exists = await this.blobs.ExistsAsync(this.blobKey, cancellationToken).ConfigureAwait(false);
            Ensure(exists);

            if (exists.Value)
            {
                throw new StorageLoadException("The temporary blob is still present.");
            }
        }
        else if (this.documents is not null)
        {
            var current = await this.documents.GetAsync(DiagnosticType, this.documentKey, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

            if (Missing(current))
            {
                this.PendingCleanup = false;

                return;
            }

            Ensure(current);
            EnsureOwner(current.Value.Properties);
            Ensure(await this.documents.DeleteAsync(DiagnosticType, this.documentKey, new() { IfMatchETag = current.Value.ETag }, cancellationToken).ConfigureAwait(false));
            var exists = await this.documents.ExistsAsync(DiagnosticType, this.documentKey, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            Ensure(exists);

            if (exists.Value)
            {
                throw new StorageLoadException("The temporary document is still present.");
            }
        }
        else
        {
            var exists = await this.files.FileExistsAsync(this.Key, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (Missing(exists))
            {
                this.PendingCleanup = false;

                return;
            }

            Ensure(exists);
            var opened = await this.files.ReadFileAsync(this.Key, cancellationToken: cancellationToken).ConfigureAwait(false);
            Ensure(opened);
            await using (var stream = opened.Value)
            {
                var header = new byte[32];
                await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

                if (!header.AsSpan().SequenceEqual(payload.AsSpan(0, header.Length)))
                {
                    throw new StorageLoadException("Temporary file ownership changed; cleanup refused.");
                }
            }

            Ensure(await this.files.DeleteFileAsync(this.Key, cancellationToken: cancellationToken).ConfigureAwait(false));
            var remaining = await this.files.FileExistsAsync(this.Key, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!Missing(remaining))
            {
                throw new StorageLoadException("Temporary file deletion could not be verified.");
            }
        }

        this.PendingCleanup = false;
        void EnsureOwner(PropertyBag properties)
        {
            var value = properties?.Get(OWNER_PROPERTY);
            var storedOwner = value switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
                _ => null
            };

            if (!string.Equals(storedOwner, this.owner, StringComparison.Ordinal))
            {
                throw new StorageLoadException("Temporary record ownership changed; cleanup refused.");
            }
        }
    }

    private static bool Missing(IResult result)
    {
        return result.IsFailure && result.Errors.Any(error => error is NotFoundError or BlobStoreNotFoundError or DocumentStoreNotFoundError);
    }

    private static void Ensure(IResult result)
    {
        if (result.Errors.Any(error => error is OperationCancelledError))
        {
            throw new OperationCanceledException();
        }

        if (result.IsFailure)
        {
            throw new StorageLoadException("Storage operation failed: " + string.Join(", ", result.Errors.Select(error => error.GetType().Name)));
        }
    }
}