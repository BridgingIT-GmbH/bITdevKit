// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEfOutbox<TContext>(this IServiceCollection services)
    where TContext : DbContext
    {
        return services.AddTransient<IOutboxMessageWriterRepository>(sp =>
            new OutboxMessageWriterRepository(
                new EntityFrameworkRepositoryOptions(sp.GetRequiredService<TContext>(),
                    sp.GetRequiredService<IEntityMapper>())
                { Autosave = true }))
            .AddTransient<IOutboxMessageWorkerRepository>(sp =>
            new OutboxMessageWorkerRepository(
                new EntityFrameworkRepositoryOptions(sp.GetRequiredService<TContext>(),
                    sp.GetRequiredService<IEntityMapper>())
                { Autosave = true }));
    }
}