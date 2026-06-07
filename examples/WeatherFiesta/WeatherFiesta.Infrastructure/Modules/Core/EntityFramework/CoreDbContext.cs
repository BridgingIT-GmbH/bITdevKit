// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using DevKit.Domain.Model;

/// <summary>
/// Entity Framework Core DbContext for the WeatherFiesta Core module.
/// Manages city, weather, user profile, and subscription entities.
/// </summary>
public class CoreDbContext(DbContextOptions<CoreDbContext> options) :
    ModuleDbContextBase(options),
    IOutboxDomainEventContext, IOrchestrationContext, IJobsContext, IMessagingContext, IQueueingContext, ILoggingContext
{
    /// <summary>Gets or sets the cities DbSet.</summary>
    public DbSet<City> Cities { get; set; }

    /// <summary>Gets or sets the user-city subscriptions DbSet.</summary>
    public DbSet<UserCity> UserCities { get; set; }

    /// <summary>Gets or sets the current weather DbSet.</summary>
    public DbSet<CurrentWeather> CurrentWeathers { get; set; }

    /// <summary>Gets or sets the weather forecasts DbSet.</summary>
    public DbSet<WeatherForecast> WeatherForecasts { get; set; }

    /// <summary>Gets or sets the user profiles DbSet.</summary>
    public DbSet<UserProfile> UserProfiles { get; set; }

    /// <summary>Gets or sets the user subscriptions DbSet.</summary>
    public DbSet<UserSubscription> UserSubscriptions { get; set; }

    /// <summary>Gets or sets the outbox domain events DbSet.</summary>
    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<BrokerMessage> BrokerMessages { get; set; }

    public DbSet<QueueMessage> QueueMessages { get; set; }

    public DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }

    public DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }

    public DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }

    public DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }

    public DbSet<JobRuntimeStateEntity> JobRuntimeStates { get; set; }

    public DbSet<JobTriggerRuntimeStateEntity> JobTriggerRuntimeStates { get; set; }

    public DbSet<JobOccurrenceEntity> JobOccurrences { get; set; }

    public DbSet<JobOccurrenceDependencyEntity> JobOccurrenceDependencies { get; set; }

    public DbSet<JobBatchEntity> JobBatches { get; set; }

    public DbSet<JobBatchOccurrenceEntity> JobBatchOccurrences { get; set; }

    public DbSet<JobExecutionEntity> JobExecutions { get; set; }

    public DbSet<JobExecutionHistoryEntity> JobExecutionHistory { get; set; }

    public DbSet<JobBatchHistoryEntity> JobBatchHistory { get; set; }

    public DbSet<JobAcceptedEventEntity> JobAcceptedEvents { get; set; }

    public DbSet<JobLeaseEntity> JobLeases { get; set; }

    public DbSet<LogEntry> LogEntries { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Soft-delete filtering is handled by specifications (e.g. UserCitiesByUserSpecification),
        // not EF global query filters. This allows IncludingDeleted specs and admin queries to work.
        base.OnModelCreating(modelBuilder);
    }
}
