// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using BridgingIT.DevKit.Infrastructure.Tests.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Text.Json;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

/// <summary>Owns a real bounded provider host and seeds inputs outside BenchmarkDotNet's measured interval.</summary>
/// <remarks>Database profiles use consumer-owned migrations. SQL Server/PostgreSQL use disposable local containers unless a dedicated BDK_CACHE_BENCH_CONNECTION is supplied.</remarks>
/// <example><code>await using var environment = await CacheBenchmarkEnvironment.CreateAsync("InMemory", 1000, 4096);</code></example>
public sealed class CacheBenchmarkEnvironment : IAsyncDisposable
{
    private readonly string directory;
    private readonly IAsyncDisposable databaseHost;
    private readonly bool ownsDirectory;
    private readonly string nativeProfile;
    private readonly SqlCommandCounter sql;
    private readonly CountingLoggerProvider logs;
    private readonly CacheBenchmarkMeasurements measurements;
    private IReadOnlyDictionary<string, IReadOnlyDictionary<CacheObservationKind, long?>> baseline;

    private CacheBenchmarkEnvironment(ServiceProvider services, string directory, int entries, int payloadBytes, IAsyncDisposable databaseHost, bool ownsDirectory,
        string nativeProfile, SqlCommandCounter sql, CountingLoggerProvider logs)
    {
        this.Services = services;
        this.directory = directory;
        this.databaseHost = databaseHost;
        this.ownsDirectory = ownsDirectory;
        this.nativeProfile = nativeProfile;
        this.sql = sql;
        this.logs = logs;
        this.EntryCount = entries;
        this.PayloadBytes = payloadBytes;
        this.Collection = services.GetRequiredService<IApplicationCache>().GetCollection<string>("values");
        this.Binary = services.GetRequiredService<IApplicationCache>().GetBinaryCollection("binary");
        this.Provider = services.GetRequiredService<CacheProviderRegistry>().Get("store");
        this.measurements = new(services.GetRequiredService<CacheRuntimeResources>());
    }

    /// <summary>Gets the owned host services and exact resource/diagnostic observations.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Gets the ordinary typed engine path under measurement.</summary>
    public ICacheCollection<string> Collection { get; }

    /// <summary>Gets the raw forward-only engine path under measurement.</summary>
    public ICacheBinaryCollection Binary { get; }

    /// <summary>Gets the complete provider for separately labeled native metadata measurements.</summary>
    public ICacheStoreProvider Provider { get; }

    /// <summary>Gets the verified initial typed entry count.</summary>
    public int EntryCount { get; }

    /// <summary>Gets the logical ASCII string size; JSON encoding adds its own envelope.</summary>
    public int PayloadBytes { get; }

    /// <summary>Gets actual executed EF database commands, including setup until MarkMeasurementsReady resets its baseline.</summary>
    public long SqlCommands => this.sql.Commands;

    /// <summary>Gets actual enabled logging records sent to the bounded counting sink, without formatting or exporter I/O.</summary>
    public long LogRecords => this.logs.Records;

    /// <summary>Gets optional benchmark-only duration and process observations, separate from BenchmarkDotNet's timed-only measurements.</summary>
    /// <example><code>environment.Measurements.RecordDuration("CallerWait", started);</code></example>
    public CacheBenchmarkMeasurements Measurements => this.measurements;

    /// <summary>Selects explicit native profiles from BDK_CACHE_BENCH_PROVIDERS or retains the workload's declared defaults.</summary>
    /// <param name="defaults">The workload's normal complete provider parameter values.</param>
    /// <returns>Distinct visible BenchmarkDotNet parameter values; an unknown profile is rejected before setup.</returns>
    /// <example><code>$env:BDK_CACHE_BENCH_PROVIDERS = 'Sqlite'; dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheProviderBenchmarks*'</code></example>
    public static IReadOnlyList<string> SelectProviders(IReadOnlyList<string> defaults)
    {
        var selected = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_PROVIDERS");
        var profiles = string.IsNullOrWhiteSpace(selected) ? defaults.ToArray() : selected.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (profiles.Length == 0 || profiles.Any(profile => profile is not ("InMemory" or "Sqlite" or "SqlServer" or "PostgreSql" or "FileSystem")))
        {
            throw new ArgumentException("BDK_CACHE_BENCH_PROVIDERS must name explicit complete native profiles.");
        }

        return profiles.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Gets explicit Plain, Compression, Jitter or Breaker result parameters from BDK_CACHE_BENCH_POLICIES.</summary>
    /// <returns>The validated visible policies, defaulting to Plain.</returns>
    public static IReadOnlyList<string> SelectPolicies()
    {
        var selected = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_POLICIES") ?? "Plain";
        var policies = selected.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (policies.Length == 0 || policies.Any(policy => policy is not ("Plain" or "Compression" or "Jitter" or "Breaker")))
        {
            throw new ArgumentException("BDK_CACHE_BENCH_POLICIES must select Plain, Compression, Jitter or Breaker.");
        }

        return policies.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Selects visible seeded entry counts through BDK_CACHE_BENCH_ENTRIES, defaulting to the specified 1,000-entry baseline.</summary>
    /// <returns>Distinct finite positive data-set sizes; reduced representatives remain labeled with their actual count.</returns>
    public static IReadOnlyList<int> SelectEntryCounts()
    {
        var selected = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_ENTRIES") ?? "1000";
        var counts = selected.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (counts.Length == 0 || counts.Any(count => count is < 1 or > 10000000)) { throw new ArgumentOutOfRangeException(nameof(selected)); }

        return counts.Distinct().ToArray();
    }

    /// <summary>Captures bounded counter baselines after workload-specific seeding and before BenchmarkDotNet warm-up or measured operations.</summary>
    /// <remarks>The cleanup log reports actual work across both warm-up and measurement; it does not pretend those counts are timed-only.</remarks>
    /// <example><code>await SeedAsync(); environment.MarkMeasurementsReady();</code></example>
    public void MarkMeasurementsReady()
    {
        this.sql.Reset();
        this.logs.Reset();
        this.measurements.Start();
        var diagnostics = this.Services.GetRequiredService<CacheDiagnostics>();
        this.baseline = this.Services.GetRequiredService<CacheCatalog>().Collections.Values.ToDictionary(binding => binding.Name,
            binding => diagnostics.ForCollection(binding.Name).Snapshot().Counters);
    }

    /// <summary>Creates a real InMemory, Sqlite, SqlServer, PostgreSql or local FileSystem profile and verifies every seed write.</summary>
    /// <param name="profile">The explicit engine/profile name.</param>
    /// <param name="entries">The bounded pre-seeded typed data set.</param>
    /// <param name="payloadBytes">The logical input bytes.</param>
    /// <param name="hot">Whether decoded local retention is explicitly enabled.</param>
    /// <param name="compression">Whether the registered payload transformation is enabled.</param>
    /// <param name="policy">One explicit healthy-store policy variant, separately visible in result parameters.</param>
    /// <param name="maximumBinaryBytes">The selected raw transfer bound; large persistent profiles require explicit matching storage capacity.</param>
    /// <param name="concurrency">The finite caller profile used to raise matching native I/O and transfer admission for concurrent workloads.</param>
    /// <param name="clock">An optional benchmark validity clock; omitted profiles retain ordinary system time and timers.</param>
    /// <param name="absoluteTtl">An optional finite absolute lifetime for controlled expiry workloads.</param>
    /// <param name="hotRepresentation">The explicitly measured retained representation.</param>
    /// <param name="directory">An optional dedicated shared local directory for independently composed application instances.</param>
    /// <param name="ownsDirectory">Whether disposal removes that dedicated directory after all peers have ended.</param>
    /// <param name="coordination">The explicit source ownership scope.</param>
    /// <param name="strategy">Shared or independent source admission.</param>
    /// <param name="providerBehavior">An optional benchmark-only provider observer or controlled fault behavior.</param>
    /// <param name="diagnosticProfile">Default preserves existing settings; Minimal disables observations, Standard adds metrics/profiling, Detailed also adds logging and bounded key history.</param>
    /// <param name="configureCollection">An optional final finite collection override for a deliberately selected pressure profile.</param>
    /// <returns>The caller-owned isolated host.</returns>
    public static async Task<CacheBenchmarkEnvironment> CreateAsync(string profile, int entries, int payloadBytes, bool hot = false, bool compression = false,
        string policy = "Plain", long maximumBinaryBytes = 2 * CacheDefaults.MiB, int concurrency = 1, TimeProvider clock = null, TimeSpan? absoluteTtl = null,
        CacheHotEntryRepresentation hotRepresentation = CacheHotEntryRepresentation.Decoded, string directory = null, bool ownsDirectory = true,
        CacheCoordinationScope coordination = CacheCoordinationScope.NodeLocal, CacheLoadingStrategy strategy = CacheLoadingStrategy.Shared,
        Func<ICacheStoreProvider, ICacheStoreProvider> providerBehavior = null, string diagnosticProfile = "Default", Action<CacheCollectionBuilder> configureCollection = null)
    {
        if (entries is < 1 or > 10000000 || payloadBytes is < 1 or > 1024 * 1024) { throw new ArgumentOutOfRangeException(nameof(entries)); }

        var persistent = profile is "Sqlite" or "SqlServer" or "PostgreSql" or "FileSystem";
        var residentBytes = ReadBoundedSetting("MAX_RESIDENT_BYTES", 512 * CacheDefaults.MiB, 16 * CacheDefaults.MiB, 4L * 1024 * CacheDefaults.GiB);
        var providerResidentBytes = ReadBoundedSetting("PROVIDER_RESIDENT_BYTES", 256 * CacheDefaults.MiB, 16 * CacheDefaults.MiB, residentBytes);
        var collectionResidentBytes = ReadBoundedSetting("COLLECTION_RESIDENT_BYTES", 256 * CacheDefaults.MiB, 16 * CacheDefaults.MiB, providerResidentBytes);
        var transientBytes = ReadBoundedSetting("MAX_TRANSIENT_BYTES", 256 * CacheDefaults.MiB, 4 * CacheDefaults.MiB, 4L * 1024 * CacheDefaults.GiB);
        var metadataBytes = ReadBoundedSetting("METADATA_BYTES", 16 * CacheDefaults.MiB, CacheDefaults.MiB, providerResidentBytes);
        var setupSeconds = ReadBoundedSetting("SETUP_TIMEOUT_SECONDS", 1200, 1, 86400);
        var progressEvery = ReadBoundedSetting("PREFILL_PROGRESS_EVERY", 10000, 1, 1000000);
        using var setup = new CancellationTokenSource(TimeSpan.FromSeconds(setupSeconds));
        var storageSetting = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_MAX_STORAGE_BYTES");
        var storageBytes = storageSetting is null ? (long?)null : long.Parse(storageSetting, System.Globalization.CultureInfo.InvariantCulture);
        var entriesSetting = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_MAX_ENTRIES");
        var maxEntries = entriesSetting is null ? Math.Max(persistent ? 1000000L : 100000L, entries + 64L) :
            long.Parse(entriesSetting, System.Globalization.CultureInfo.InvariantCulture);
        if (storageBytes is <= 0 or > 4L * 1024 * CacheDefaults.GiB || maxEntries < entries + 64L || maxEntries > 1000000000L)
        {
            throw new ArgumentOutOfRangeException(nameof(entries), "Storage must be finite and at most four TiB; entry capacity must leave at least 64 entries beyond the seeded data set and be at most one billion.");
        }

        directory ??= Path.Combine(Path.GetTempPath(), "bdk-cache-benchmark-" + Guid.NewGuid().ToString("N"));
        if (diagnosticProfile is not ("Default" or "Minimal" or "Standard" or "Detailed")) { throw new ArgumentException("Unknown diagnostic profile.", nameof(diagnosticProfile)); }

        Directory.CreateDirectory(directory);
        var services = new ServiceCollection();
        IAsyncDisposable databaseHost = null;
        services.AddLogging();
        services.AddSingleton(clock ?? TimeProvider.System);
        var sql = new SqlCommandCounter();
        var logs = new CountingLoggerProvider();
        if (diagnosticProfile == "Detailed")
        {
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning)
                .AddFilter("BridgingIT.DevKit.Application.Caching", LogLevel.Debug).AddProvider(logs));
        }

        var durability = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_DURABILITY") ?? "Durable";
        if (durability is not ("Durable" or "Buffered")) { throw new ArgumentException("Unknown native durability profile."); }

        var cache = services.AddApplicationCaching().WithOptions(o => o.With(x => x.Namespace, "benchmark")
            .With(x => x.Limits.MaxResidentBytes, residentBytes).With(x => x.Limits.MaxTransientBytes, transientBytes)
            .With(x => x.Limits.MaxActiveProviderOperations, Math.Max(64, concurrency)).With(x => x.Limits.MaxTransfers, Math.Max(8, concurrency))
            .With(x => x.Limits.MaxActiveSources, Math.Max(32, concurrency)).With(x => x.Diagnostics.Enabled, diagnosticProfile != "Minimal")
            .With(x => x.Diagnostics.DetailedKeyHistory, diagnosticProfile == "Detailed"))
            .WithValueContract<string>("text", c => c.WithImmutableSharing(value => 64L + (value?.Length ?? 0) * 2L))
            .WithBinaryContract("raw")
            .WithProvider("store", p => p.With(x => x.Kind, providerBehavior is not null ? "BenchmarkObserved" : profile is "Sqlite" or "SqlServer" or "PostgreSql" ? "EntityFramework" : profile)
                .With(x => x.Engine, profile is "Sqlite" or "SqlServer" or "PostgreSql" ? profile : "Sqlite")
                .With(x => x.Durability, durability)
                .With(x => x.RootPath, directory).With(x => x.MaxResidentBytes, providerResidentBytes).With(x => x.MetadataCacheBytes, metadataBytes).With(x => x.Breaker.Enabled, policy == "Breaker")
                .With(x => x.MaxStorageBytes, storageBytes ?? 10 * CacheDefaults.GiB).With(x => x.MaxEntries, maxEntries))
            .Defaults(c => c.With(x => x.Provider, "store").With(x => x.Expiration.AbsoluteTtl, absoluteTtl ?? TimeSpan.FromHours(12))
                .With(x => x.Limits.MaxStorageBytes, storageBytes ?? (persistent ? 10 * CacheDefaults.GiB : 128 * CacheDefaults.MiB))
                .With(x => x.Limits.MaxEntries, maxEntries)
                .With(x => x.Limits.MaxResidentBytes, collectionResidentBytes).With(x => x.Limits.MaxEncodedBytes, 2 * CacheDefaults.MiB)
                .With(x => x.Limits.MaxLogicalBytes, 2 * CacheDefaults.MiB).With(x => x.HotEntries.Enabled, hot)
                .With(x => x.Limits.MaxDecodedBytes, 4 * CacheDefaults.MiB)
                .With(x => x.Limits.MaxActiveProviderOperations, Math.Max(16, concurrency)).With(x => x.Limits.MaxTransfers, Math.Max(4, concurrency))
                .With(x => x.Limits.MaxQueuedProviderOperations, Math.Max(64, concurrency * 2))
                .With(x => x.HotEntries.Representation, hotRepresentation)
                .With(x => x.Loading.CoordinationScope, coordination).With(x => x.Loading.Strategy, strategy)
                .With(x => x.Limits.MaxActiveSources, Math.Max(8, concurrency))
                .With(x => x.Expiration.JitterRatio, policy == "Jitter" ? 0.2 : 0)
                .With(x => x.Compression.Enabled, compression || policy == "Compression").With(x => x.Compression.Algorithm, CacheCompressionAlgorithm.GZip)
                .With(x => x.Compression.Level, CompressionLevel.Optimal).With(x => x.NegativeCaching.Enabled, true)
                .With(x => x.NegativeCaching.Ttl, TimeSpan.FromHours(1)))
            .WithCollection<string>("values", configureCollection).WithBinaryCollection("binary", c => c.With(x => x.HotEntries.Enabled, false)
                .With(x => x.Limits.MaxEncodedBytes, Math.Max(2 * CacheDefaults.MiB, maximumBinaryBytes))
                .With(x => x.Limits.MaxLogicalBytes, Math.Max(2 * CacheDefaults.MiB, maximumBinaryBytes)));
        if (profile == "FileSystem") { cache.WithFileSystemProvider(); }
        else if (profile is "Sqlite" or "SqlServer" or "PostgreSql")
        {
            var connection = profile == "Sqlite" ? "Data Source=" + Path.Combine(directory, "cache.db") + ";Pooling=False" :
                Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection) && profile == "SqlServer")
            {
                var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await container.StartAsync(setup.Token);
                databaseHost = container;
                var database = "cachebench_" + Guid.NewGuid().ToString("N");
                await using var bootstrapConnection = new Microsoft.Data.SqlClient.SqlConnection(container.GetConnectionString());
                await bootstrapConnection.OpenAsync(setup.Token);
                await using var command = bootstrapConnection.CreateCommand();
                command.CommandText = "CREATE DATABASE [" + database + "]";
                await command.ExecuteNonQueryAsync(setup.Token);
                connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = database }.ToString();
            }
            else if (string.IsNullOrWhiteSpace(connection) && profile == "PostgreSql")
            {
                var container = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("cachebench").Build();
                await container.StartAsync(setup.Token);
                databaseHost = container;
                connection = container.GetConnectionString();
            }

            var options = new DbContextOptionsBuilder<CacheTestHostDbContext>();
            options.AddInterceptors(sql);
            var assembly = typeof(CacheTestHostDbContext).Assembly.GetName().Name;
            if (profile == "Sqlite") { options.UseSqlite(connection, b => b.MigrationsAssembly(assembly)); }
            else if (profile == "SqlServer") { options.UseSqlServer(connection, b => b.MigrationsAssembly(assembly)); }
            else { options.UseNpgsql(connection, b => b.MigrationsAssembly(assembly)); }

            CacheTestHostDbContext Context()
            {
                var context = new CacheTestHostDbContext(options.Options);
                context.Database.OpenConnection();
                if (profile == "Sqlite") { context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;"); }

                return context;
            }

            await using (var bootstrap = Context())
            {
                await bootstrap.Database.MigrateAsync(setup.Token);
                if (profile == "Sqlite") { await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", setup.Token); }
            }

            cache.WithEntityFrameworkProvider((_, _, _) => Context());
        }
        else if (profile != "InMemory") { throw new ArgumentException("Unknown complete provider profile.", nameof(profile)); }

        if (diagnosticProfile is "Standard" or "Detailed")
        {
            services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
            cache.WithMetrics().WithProfiling();
        }

        if (diagnosticProfile == "Detailed") { cache.WithLogging(); }

        if (providerBehavior is not null)
        {
            var nativeKind = cache.Components.ProviderKinds[profile is "Sqlite" or "SqlServer" or "PostgreSql" ? "EntityFramework" : profile];
            cache.WithProviderKind("BenchmarkObserved", nativeKind.CapabilityResolver ?? (_ => nativeKind.Capabilities),
                (sp, name, options) => providerBehavior(nativeKind.Factory(sp, name, options)));
        }

        var environment = new CacheBenchmarkEnvironment(services.BuildServiceProvider(), directory, entries, payloadBytes, databaseHost, ownsDirectory, profile, sql, logs);
        var profilingWriter = environment.Services.GetService<OperationProfilingWriterService>();
        if (profilingWriter is not null) { await profilingWriter.StartAsync(default); }

        try
        {
            var value = new string('x', payloadBytes);
            var started = Stopwatch.GetTimestamp();
            Console.WriteLine($"[cache-benchmark-prefill] profile={profile} entries={entries} payloadBytes={payloadBytes} processResident={residentBytes} providerResident={providerResidentBytes} collectionResident={collectionResidentBytes} metadata={metadataBytes} setupSeconds={setupSeconds}");
            for (var index = 0; index < entries; index++)
            {
                setup.Token.ThrowIfCancellationRequested();
                var written = await environment.Collection.SetAsync(new("seed:" + index), value, cancellationToken: setup.Token);
                Ensure(written);
                if (written.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("Seeding did not store every declared entry."); }

                if ((index + 1) % progressEvery == 0 || index + 1 == entries)
                {
                    Console.WriteLine($"[cache-benchmark-prefill] stored={index + 1}/{entries} elapsedSeconds={Stopwatch.GetElapsedTime(started).TotalSeconds:F3}");
                }
            }

            var quota = await environment.Provider.GetQuotaAsync(setup.Token);
            Ensure(quota);
            if (quota.Value.CurrentEntries != entries) { throw new InvalidDataException($"The requested {entries}-entry data set retained {quota.Value.CurrentEntries} entries; capacity loss or an existing data set invalidates this case."); }

            if (profilingWriter is not null) { await profilingWriter.TickAsync(); }

            return environment;
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    /// <summary>Throws on failed or incomplete measured work instead of counting it as successful throughput.</summary>
    /// <param name="result">The observed cache operation result.</param>
    public static void Ensure(IResult result)
    {
        if (result.IsFailure) { throw new InvalidDataException(string.Join("; ", result.Errors.Select(error => error.Message))); }
    }

    private static long ReadBoundedSetting(string suffix, long fallback, long minimum, long maximum)
    {
        var name = "BDK_CACHE_BENCH_" + suffix;
        var setting = Environment.GetEnvironmentVariable(name);
        var value = setting is null ? fallback : long.Parse(setting, System.Globalization.CultureInfo.InvariantCulture);
        if (value < minimum || value > maximum) { throw new ArgumentOutOfRangeException(name, $"Expected a finite value from {minimum} through {maximum}."); }

        return value;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var catalog = this.Services.GetRequiredService<CacheCatalog>();
        var resources = this.Services.GetRequiredService<CacheRuntimeResources>();
        var diagnostics = this.Services.GetRequiredService<CacheDiagnostics>();
        var executionMeasurements = this.measurements.Finish();
        var native = await this.Provider.GetQuotaAsync();
        Console.WriteLine("[cache-benchmark-observations] " + JsonSerializer.Serialize(new
        {
            Kind = catalog.Providers["store"].Kind.Alias,
            Engine = this.nativeProfile is "Sqlite" or "SqlServer" or "PostgreSql" ? catalog.Providers["store"].Options.Engine : null,
            Durability = this.nativeProfile == "FileSystem" ? catalog.Providers["store"].Options.Durability : null,
            AccessKind = this.nativeProfile == "FileSystem" ? catalog.Providers["store"].Options.AccessKind : null,
            NativeProfile = this.nativeProfile,
            SqlCommands = this.SqlCommands,
            Logging = new { Records = this.LogRecords, Sink = "CountingWithoutFormattingOrExport" },
            Entries = this.EntryCount,
            LogicalPayloadBytes = this.PayloadBytes,
            Clock = new
            {
                Mode = this.Services.GetRequiredService<TimeProvider>() is CacheBenchmarkClock ? "AdvancedValidityWithSystemTimers" : "System",
                UtcNow = this.Services.GetRequiredService<TimeProvider>().GetUtcNow(),
                AdvancedValidityTicks = (this.Services.GetRequiredService<TimeProvider>() as CacheBenchmarkClock)?.AdvancedTicks ?? 0
            },
            ProviderLimits = new { catalog.Providers["store"].Options.MaxEntries, catalog.Providers["store"].Options.MaxStorageBytes, catalog.Providers["store"].Options.MaxResidentBytes },
            CollectionPolicy = catalog.Collections["values"].Options,
            Limits = catalog.Options.Limits,
            CountsIncludeWarmupAndMeasurement = true,
            ExecutionMeasurements = executionMeasurements,
            Collections = catalog.Collections.Values.Select(binding =>
            {
                var observations = diagnostics.ForCollection(binding.Name);
                return new
                {
                    Collection = binding.Name,
                    CounterDeltas = observations.Snapshot().Counters.ToDictionary(pair => pair.Key,
                        pair => pair.Value.HasValue && this.baseline?[binding.Name][pair.Key] is { } before ? pair.Value - before : null),
                    Latencies = observations.Snapshot().Latencies,
                    LatenciesIncludeSetup = true,
                    HotFootprint = observations.HotFootprint?.Snapshot()
                };
            }).ToArray(),
            ResidentBytesAfter = resources.Resident.Used,
            TransientBytesAfter = resources.Transient.Used,
            HotResidentBytesAfter = resources.HotResident.Used,
            WorkingSetBytesAfter = Environment.WorkingSet,
            ActiveNativeTailsAfter = this.Services.GetRequiredService<CacheRuntimeTasks>().Count,
            NativeQuota = native.IsSuccess ? native.Value : null,
            NativeQuotaAvailable = native.IsSuccess
        }));
        var profilingWriter = this.Services.GetService<OperationProfilingWriterService>();
        if (profilingWriter is not null)
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await profilingWriter.StopAsync(stop.Token);
        }

        await this.Services.DisposeAsync();
        if (this.databaseHost is not null) { await this.databaseHost.DisposeAsync(); }

        if (this.ownsDirectory && Directory.Exists(this.directory))
        {
            var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var owned = Path.GetFullPath(this.directory);
            if (!owned.StartsWith(temporary, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !Path.GetFileName(owned).StartsWith("bdk-cache-", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Benchmark cleanup requires its dedicated OS temporary directory.");
            }

            BridgingIT.DevKit.Infrastructure.FileSystem.FileSystemPaths.RejectLinks(owned);
            Directory.Delete(owned, recursive: true);
        }
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        private long commands;

        /// <summary>Gets actual executed native SQL command-entry callbacks.</summary>
        public long Commands => Interlocked.Read(ref this.commands);

        /// <summary>Starts a new counter cohort after preparation.</summary>
        public void Reset() => Interlocked.Exchange(ref this.commands, 0);

        /// <inheritdoc />
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref this.commands);
            return result;
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(this.ReaderExecuting(command, eventData, result));

        /// <inheritdoc />
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref this.commands);
            return result;
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(this.NonQueryExecuting(command, eventData, result));

        /// <inheritdoc />
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Interlocked.Increment(ref this.commands);
            return result;
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default) => ValueTask.FromResult(this.ScalarExecuting(command, eventData, result));
    }

    private sealed class CountingLoggerProvider : ILoggerProvider
    {
        private long records;

        /// <summary>Gets enabled log records delivered to the real counting sink.</summary>
        public long Records => Interlocked.Read(ref this.records);

        /// <summary>Starts a new logging cohort after preparation.</summary>
        public void Reset() => Interlocked.Exchange(ref this.records, 0);

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new CounterLogger(this);

        /// <inheritdoc />
        public void Dispose() { }

        private sealed class CounterLogger(CountingLoggerProvider owner) : ILogger
        {
            /// <inheritdoc />
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            /// <inheritdoc />
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

            /// <inheritdoc />
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
                Interlocked.Increment(ref owner.records);
        }
    }
}