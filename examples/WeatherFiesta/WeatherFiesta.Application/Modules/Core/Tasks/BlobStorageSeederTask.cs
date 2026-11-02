// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Tasks;

using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>Seeds a sample blob so the weather container is available on the first dashboard visit.</summary>
/// <example><code>startupTasks.WithTask&lt;BlobStorageSeederTask&gt;();</code></example>
public sealed class BlobStorageSeederTask(IDatabaseReadyService databaseReadyService, IBlobStoreClientFactory clients) : IStartupTask
{
    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await databaseReadyService.WaitForReadyAsync(timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken).ConfigureAwait(false);
        var client = clients.CreateClient("default");
        var key = new BlobKey("weather", "welcome.txt");
        var existing = await client.ExistsAsync(key, cancellationToken).ConfigureAwait(false);

        if (existing.IsFailure)
        {
            throw new InvalidOperationException("The default weather blob could not be checked.");
        }

        if (existing.Value)
        {
            return;
        }

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("WeatherFiesta blob storage is ready.\n"));
        var created = await client.UploadAsync(new()
        {
            Key = key,
            Content = content,
            OverwriteMode = BlobOverwriteMode.FailIfExists
        }, cancellationToken).ConfigureAwait(false);

        if (created.IsFailure && !created.Errors.Any(error => error is BlobStoreConflictError))
        {
            throw new InvalidOperationException("The default weather blob could not be seeded.");
        }
    }
}