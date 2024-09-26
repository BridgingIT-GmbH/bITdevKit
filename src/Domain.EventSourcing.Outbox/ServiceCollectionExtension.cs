// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.EventSourcing.Outbox;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddEfOutboxWorker(this IServiceCollection services)
    {
        services.AddTransient<IOutboxWorkerService, OutboxWorkerService>();

        return services;
    }
}