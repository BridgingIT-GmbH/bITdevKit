// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure;

using DevKit.Infrastructure.EntityFramework;
using DevKit.Infrastructure.Mapping;
using Domain.Repositories;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repositories;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventSourcingDemoDbContextSqlServer(
        this IServiceCollection services,
        string connectionString)
    {
        var options = new SqlServerOptions
        {
            ConnectionString = connectionString, // <2>
            MigrationsAssemblyName = typeof(EventSourcingDemoDbContext).Assembly.FullName, // <3>
            LoggerEnabled = true, // <6>
            SensitiveDataLoggingEnabled = true, // <7>
            DetailedErrorsEnabled = true // <8>
        };

        services.AddSqlServerDbContext<EventSourcingDemoDbContext>(options); // <1>
        services.AddScoped<IPersonOverviewRepository>(sp =>
            new PersonOverviewRepository(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .DbContext(sp.GetRequiredService<EventSourcingDemoDbContext>())
                .Mapper(new MapsterEntityMapper(sp.GetService<IMapper>()))));

        return services;
    }
}