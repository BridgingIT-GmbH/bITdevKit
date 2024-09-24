// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Diagnostics.HealthChecks;

public static class CosmosClientBuilderContextExtensions
{
    public static CosmosClientBuilderContext WithHealthChecks(
        this CosmosClientBuilderContext context,
        string healthQuery = default,
        string name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = default)
    {
        //context.Services.AddHealthChecks()
        //    .AddAzureCosmosDB(context.ConnectionString, null, name ?? $"cosmosdb ({context.AccountName})", failureStatus, tags ?? new[] { "ready" }, timeout);
        // TODO: This does not add a health check as method implies.
        return context;
    }
}