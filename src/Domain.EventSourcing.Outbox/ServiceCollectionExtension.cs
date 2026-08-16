// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.EventSourcing.Outbox;

/// <summary>
/// Represents service collection extension.
/// </summary>
public static class ServiceCollectionExtension
{
    /// <summary>
    /// Adds ef outbox worker.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddEfOutboxWorker(this IServiceCollection services)
    {
        services.AddTransient<IOutboxWorkerService, OutboxWorkerService>();

        return services;
    }
}
