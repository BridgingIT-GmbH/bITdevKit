// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static InMemoryRepositoryBuilderContext<TEntity, InMemoryContext<TEntity>> AddInMemoryRepository<TEntity>(
        this IServiceCollection services,
        InMemoryContext<TEntity> context = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (context is not null)
        {
            services.AddSingleton(context);
        }
        else
        {
            services.AddSingleton(new InMemoryContext<TEntity>());
        }

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericRepository<TEntity>),
                    typeof(InMemoryRepositoryWrapper<TEntity, InMemoryContext<TEntity>>));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericRepository<TEntity>),
                    typeof(InMemoryRepositoryWrapper<TEntity, InMemoryContext<TEntity>>));

                break;
            default:
                services.AddScoped(typeof(IGenericRepository<TEntity>),
                    typeof(InMemoryRepositoryWrapper<TEntity, InMemoryContext<TEntity>>));

                break;
        }

        return new InMemoryRepositoryBuilderContext<TEntity, InMemoryContext<TEntity>>(services, lifetime);
    }

    public static InMemoryRepositoryBuilderContext<TEntity, TContext> AddInMemoryRepository<TEntity, TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TContext : InMemoryContext<TEntity>
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericRepository<TEntity>),
                    typeof(InMemoryRepositoryWrapper<TEntity, TContext>));

                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericRepository<TEntity>),
                    typeof(InMemoryRepositoryWrapper<TEntity, TContext>));

                break;
            default:
                services.AddScoped(typeof(IGenericRepository<TEntity>),
                    typeof(InMemoryRepositoryWrapper<TEntity, TContext>));

                break;
        }

        return new InMemoryRepositoryBuilderContext<TEntity, TContext>(services, lifetime);
    }

    public static InMemoryRepositoryBuilderContext<TEntity, TContext>
        WithSequenceNumberGenerator<TEntity, TContext>(
            this InMemoryRepositoryBuilderContext<TEntity, TContext> context,
            string sequenceName,
            long startValue = 1,
            int increment = 1,
            long minValue = 1,
            long maxValue = long.MaxValue,
            bool isCyclic = false,
            string schema = null,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TEntity : class, IEntity
        where TContext : InMemoryContext<TEntity>
    {
        if (string.IsNullOrEmpty(sequenceName))
        {
            throw new ArgumentException("sequenceName is required", nameof(sequenceName));
        }

        ArgumentOutOfRangeException.ThrowIfZero(increment);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        if (startValue < minValue || startValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(startValue));
        }

        // accumulate configurations
        context.Services.AddSingleton(new InMemorySequenceNumberGeneratorConfiguration(
            sequenceName, startValue, increment, minValue, maxValue, isCyclic, schema
        ));

        // ensure generator is only registered once; apply all configurations when constructed
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                context.Services.TryAddSingleton<ISequenceNumberGenerator>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var instance = new InMemorySequenceNumberGenerator(loggerFactory);
                    ApplyAllConfigurations(sp, instance);
                    return instance;
                });
                break;

            case ServiceLifetime.Transient:
                // not typical for sequences, but if needed, apply all configs each resolve
                context.Services.TryAddTransient<ISequenceNumberGenerator>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var instance = new InMemorySequenceNumberGenerator(loggerFactory);
                    ApplyAllConfigurations(sp, instance);
                    return instance;
                });
                break;

            default: // Scoped
                context.Services.TryAddScoped<ISequenceNumberGenerator>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var instance = new InMemorySequenceNumberGenerator(loggerFactory);
                    ApplyAllConfigurations(sp, instance);
                    return instance;
                });
                break;
        }

        return context;

        static void ApplyAllConfigurations(IServiceProvider sp, InMemorySequenceNumberGenerator instance)
        {
            foreach (var configuration in sp.GetServices<InMemorySequenceNumberGeneratorConfiguration>())
            {
                instance.ConfigureSequence(configuration.SequenceName, configuration.StartValue, configuration.Increment, configuration.MinValue, configuration.MaxValue, configuration.IsCyclic, configuration.Schema);
            }
        }
    }

    public sealed record InMemorySequenceNumberGeneratorConfiguration(string SequenceName, long StartValue, int Increment, long MinValue, long MaxValue, bool IsCyclic, string Schema);
}