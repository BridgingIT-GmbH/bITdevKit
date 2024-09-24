// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Diagnostics.HealthChecks;

public static class AzureStorageBuilderContextExtensions
{
    public static AzureStorageBuilderContext WithHealthChecks(
        this AzureStorageBuilderContext context,
        string healthQuery = default,
        string name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = default)
    {
        if (context.Service == Service.Blob)
        {
            context.Services.AddHealthChecks()
                .AddAzureBlobStorage(context.ConnectionString,
                    name: name ?? $"blobstorage ({context.AccountName})",
                    failureStatus: failureStatus,
                    tags: tags ?? new[] { "ready" });
        }
        else if (context.Service == Service.Table)
        {
            context.Services.AddHealthChecks()
                .AddAzureBlobStorage(context.ConnectionString,
                    name: name ?? $"tablestorage ({context.AccountName})",
                    failureStatus: failureStatus,
                    tags: tags ?? new[] { "ready" });
        }
        else if (context.Service == Service.Queue)
        {
            context.Services.AddHealthChecks()
                .AddAzureQueueStorage(context.ConnectionString,
                    name: name ?? $"queuestorage ({context.AccountName})",
                    failureStatus: failureStatus,
                    tags: tags ?? new[] { "ready" });
        }

        return context;
    }
}