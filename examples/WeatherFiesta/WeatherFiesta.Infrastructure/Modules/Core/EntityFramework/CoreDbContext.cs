// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

/// <summary>
/// Entity Framework Core DbContext for the WeatherFiesta Core module.
/// Manages city, weather, user profile, and subscription entities.
/// </summary>
/// <param name="options">The host-owned Core database options.</param>
public class CoreDbContext(DbContextOptions<CoreDbContext> options) :
    ModuleDbContextBase(options),
    IOutboxDomainEventContext, IOrchestrationContext, IJobsContext, IMessagingContext, IQueueingContext, ILoggingContext, IFileStorageContext, IDocumentStoreContext, IBlobStoreContext, IStoragePermalinkRegistryContext, IBroadcastingContext, IAuditingDbContext, IApplicationCachingContext
{
    /// <inheritdoc />
    public DbSet<CacheStoreEntity> CacheStores { get; set; }

    /// <inheritdoc />
    public DbSet<CacheCollectionEntity> CacheCollections { get; set; }

    /// <inheritdoc />
    public DbSet<CacheContractEntity> CacheContracts { get; set; }

    /// <inheritdoc />
    public DbSet<CacheKeyEntity> CacheKeys { get; set; }

    /// <inheritdoc />
    public DbSet<CacheEntryEntity> CacheEntries { get; set; }

    /// <inheritdoc />
    public DbSet<CachePayloadEntity> CachePayloads { get; set; }

    /// <inheritdoc />
    public DbSet<CacheChunkEntity> CacheChunks { get; set; }

    /// <inheritdoc />
    public DbSet<CacheDimensionEntity> CacheDimensions { get; set; }

    /// <inheritdoc />
    public DbSet<CacheFenceEntity> CacheFences { get; set; }

    /// <inheritdoc />
    public DbSet<CacheOwnershipEntity> CacheOwnerships { get; set; }

    /// <inheritdoc />
    public DbSet<CacheReaderEntity> CacheReaders { get; set; }

    /// <inheritdoc />
    public DbSet<CacheOperationEntity> CacheOperations { get; set; }

    /// <summary>Gets or sets the cities DbSet.</summary>
    public DbSet<City> Cities { get; set; }

    /// <summary>Gets or sets the user-city subscriptions DbSet.</summary>
    public DbSet<UserCity> UserCities { get; set; }

    /// <summary>Gets or sets the current weather DbSet.</summary>
    public DbSet<CurrentWeather> CurrentWeathers { get; set; }

    /// <summary>Gets or sets the weather forecasts DbSet.</summary>
    public DbSet<WeatherForecast> WeatherForecasts { get; set; }

    /// <summary>Gets or sets the generated weather reports DbSet.</summary>
    public DbSet<WeatherReport> WeatherReports { get; set; }

    /// <summary>Gets or sets the user profiles DbSet.</summary>
    public DbSet<UserProfile> UserProfiles { get; set; }

    /// <summary>Gets or sets the user subscriptions DbSet.</summary>
    public DbSet<UserSubscription> UserSubscriptions { get; set; }

    /// <summary>Gets or sets the outbox domain events DbSet.</summary>
    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    /// <inheritdoc />
    public DbSet<BrokerMessage> BrokerMessages { get; set; }

    /// <inheritdoc />
    public DbSet<QueueMessage> QueueMessages { get; set; }

    /// <inheritdoc />
    public DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }

    /// <inheritdoc />
    public DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }

    /// <inheritdoc />
    public DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }

    /// <inheritdoc />
    public DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }

    /// <inheritdoc />
    public DbSet<JobRuntimeStateEntity> JobRuntimeStates { get; set; }

    /// <inheritdoc />
    public DbSet<JobTriggerRuntimeStateEntity> JobTriggerRuntimeStates { get; set; }

    /// <inheritdoc />
    public DbSet<JobOccurrenceEntity> JobOccurrences { get; set; }

    /// <inheritdoc />
    public DbSet<JobOccurrenceDependencyEntity> JobOccurrenceDependencies { get; set; }

    /// <inheritdoc />
    public DbSet<JobBatchEntity> JobBatches { get; set; }

    /// <inheritdoc />
    public DbSet<JobBatchOccurrenceEntity> JobBatchOccurrences { get; set; }

    /// <inheritdoc />
    public DbSet<JobExecutionEntity> JobExecutions { get; set; }

    /// <inheritdoc />
    public DbSet<JobExecutionHistoryEntity> JobExecutionHistory { get; set; }

    /// <inheritdoc />
    public DbSet<JobBatchHistoryEntity> JobBatchHistory { get; set; }

    /// <inheritdoc />
    public DbSet<JobAcceptedEventEntity> JobAcceptedEvents { get; set; }

    /// <inheritdoc />
    public DbSet<JobLeaseEntity> JobLeases { get; set; }

    /// <inheritdoc />
    public DbSet<FileStorageFileEntity> StorageFiles { get; set; }

    /// <inheritdoc />
    public DbSet<FileStorageFileContentEntity> StorageFileContents { get; set; }

    /// <inheritdoc />
    public DbSet<FileStorageDirectoryEntity> StorageDirectories { get; set; }

    /// <inheritdoc />
    public DbSet<StorageBlob> StorageBlobs { get; set; }

    /// <inheritdoc />
    public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

    /// <inheritdoc />
    public DbSet<StorageDocument> StorageDocuments { get; set; }

    /// <inheritdoc />
    public DbSet<StoragePermalink> StoragePermalinks { get; set; }

    /// <inheritdoc />
    public DbSet<LogEntry> LogEntries { get; set; }

    /// <inheritdoc />
    public DbSet<AuditRecordEntity> AuditRecords { get; set; }

    /// <inheritdoc />
    public DbSet<AuditActionEntity> AuditActions { get; set; }

    /// <inheritdoc />
    public DbSet<AuditEntityChangeEntity> AuditEntityChanges { get; set; }

    /// <inheritdoc />
    public DbSet<AuditStoreStateEntity> AuditStoreState { get; set; }

    /// <inheritdoc />
    public DbSet<BroadcastNodeRegistrationEntity> BroadcastNodeRegistrations { get; set; }

    /// <inheritdoc />
    public DbSet<BroadcastNodeScopeEntity> BroadcastNodeScopes { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Soft-delete filtering is handled by specifications (e.g. UserCitiesByUserSpecification),
        // not EF global query filters. This allows IncludingDeleted specs and admin queries to work.
        base.OnModelCreating(modelBuilder);
        if (!this.Database.IsRelational()) return;

        // Host-owned initialization is part of the sample's EnsureCreated schema.
        modelBuilder.Entity<AuditStoreStateEntity>().Property(state => state.StoreId).HasDefaultValueSql("NEWID()");
        modelBuilder.Entity<AuditStoreStateEntity>().Property(state => state.StoreEpoch).HasDefaultValueSql("NEWID()");
        modelBuilder.Entity<AuditStoreStateEntity>().HasData(new
        {
            Singleton = 1, PublishedPosition = 0L, LifecycleGeneration = 0L, RemovedThroughPosition = 0L,
            RejectCompletedBeforeUtcTicks = 0L, ArchiveCompletedBeforeUtcTicks = 0L
        });
    }
}