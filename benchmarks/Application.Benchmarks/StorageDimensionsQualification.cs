// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Common;
using Application.Storage;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

/// <summary>Runs explicit, resource-bounded storage Dimensions workloads outside BenchmarkDotNet.</summary>
/// <example><code>await StorageDimensionsQualification.RunAsync(["--engine", "sqlite", "--feature", "blob", "--owners", "10000"]);</code></example>
public static partial class StorageDimensionsQualification
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    /// <summary>Runs the requested growing-dataset case and writes evidence into a new output directory.</summary>
    /// <param name="args">Engine, feature, owner count, fixed seed, and optional output directory.</param>
    /// <returns>Zero when the executed operations pass; the report separately records incomplete qualification gates.</returns>
    /// <example><code>var exitCode = await StorageDimensionsQualification.RunAsync(args);</code></example>
    public static async Task<int> RunAsync(string[] args)
    {
        var budgeted = StorageDimensionsQualificationBudget.StartBudgetedChild(args);
        if (budgeted.HasValue)
        {
            return budgeted.Value;
        }

        var engine = Argument(args, "--engine", "sqlite");
        var feature = Argument(args, "--feature", "combined");
        var owners = int.Parse(Argument(args, "--owners", "10000"), System.Globalization.CultureInfo.InvariantCulture);
        var seed = int.Parse(Argument(args, "--seed", "17043"), System.Globalization.CultureInfo.InvariantCulture);
        var profile = Argument(args, "--profile", "reference");
        if (args.Contains("--storage-item-writes"))
        {
            if (engine is not ("sqlserver" or "postgres") || feature != "combined" || profile != "reference")
            {
                throw new ArgumentException("Item writes require combined SQL Server/PostgreSQL reference fixtures.");
            }

            return await RunItemWritesAsync(engine, Argument(args, "--output", null), args.Contains("--smoke"), args.Contains("--bulk-uploads-only"), args.Contains("--owners") ? owners : null);
        }

        if (engine is not ("sqlserver" or "postgres" or "sqlite") || feature is not ("blob" or "document" or "combined") || owners is not (10000 or 100000 or 1000000) || seed != 17043 || profile is not ("reference" or "sparse" or "skew"))
        {
            throw new ArgumentException("Use declared engine/feature profiles, 10000/100000/1000000 owners, and seed 17043.");
        }

        var ownerIndexProbe = args.Contains("--probe-owner-visibility-index");
        if (ownerIndexProbe && (engine != "sqlserver" || feature != "combined"))
        {
            throw new ArgumentException("Owner-index experiments require the combined SQL Server fixture.");
        }

        var contentionProfile = args.Contains("--dimension-contention-profile");
        var randomReads = args.Contains("--storage-random-reads");
        var updateRecheck = args.Contains("--dimension-update-recheck");
        if ((randomReads || updateRecheck || contentionProfile || ownerIndexProbe) && (engine == "sqlite" || profile != "reference" || args.Any(value => value is "--scaling" or "--stress" or "--profile-mixed" or "--profile-offered-load")))
        {
            throw new ArgumentException("Read scaling and update rechecks require an isolated reference server workload.");
        }

        if (randomReads && (updateRecheck || contentionProfile || feature == "combined" || args.Contains("--restore-seed")))
        {
            throw new ArgumentException("Random reads require one feature and a fresh content fixture.");
        }

        if (contentionProfile && (feature != "combined" || updateRecheck))
        {
            throw new ArgumentException("Contention diagnostics require the combined workload without update recheck.");
        }

        if (ownerIndexProbe && (contentionProfile || updateRecheck || randomReads))
        {
            throw new ArgumentException("Owner-index probes cannot combine workload modes.");
        }

        var offeredProfile = Argument(args, "--profile-offered-load", null);
        var diagnostic = args.Contains("--storage-dimensions-diagnostic") || args.Contains("--profile-mixed") || offeredProfile is not null;
        var smoke = args.Contains("--smoke") || diagnostic;
        var scaling = args.Contains("--scaling");
        if (scaling && smoke)
        {
            throw new ArgumentException("Scaling requires full measured query intervals, not short diagnostic intervals.");
        }

        return await RunCaseAsync(engine, feature, smoke && !diagnostic && !contentionProfile ? 1000 : owners,
            Argument(args, "--output", Path.Combine("artifacts", "storage-dimensions", $"{DateTime.UtcNow:yyyyMMddTHHmmss}-{Guid.NewGuid():N}")), smoke, profile, args.Contains("--stress"), scaling, Argument(args, "--restore-seed", null), args.Contains("--probe-union-all"), int.Parse(Argument(args, "--sql-cost-threshold", "5"), System.Globalization.CultureInfo.InvariantCulture), args.Contains("--profile-mixed"), int.Parse(Argument(args, "--sql-max-dop", "0"), System.Globalization.CultureInfo.InvariantCulture), offeredProfile, args.Contains("--skip-profile-calibration"), int.Parse(Argument(args, "--offered-profile-pairs", "5"), System.Globalization.CultureInfo.InvariantCulture), int.Parse(Argument(args, "--ordinary-comparison-pairs", "3"), System.Globalization.CultureInfo.InvariantCulture), randomReads, updateRecheck, contentionProfile, ownerIndexProbe);
    }

    /// <summary>Runs a direct 1,000-owner smoke case by default, explicitly separate from volume qualification.</summary>
    /// <param name="engine">A declared relational engine.</param>
    /// <param name="feature">Blob, document, or combined.</param>
    /// <param name="owners">Fixture owner count per selected feature.</param>
    /// <param name="output">A new run-owned evidence directory.</param>
    /// <param name="smoke">Whether to execute only the short harness proof.</param>
    /// <param name="profile">Reference, ten-percent populated sparse, or ninety-one-percent hot-scope data.</param>
    /// <param name="stress">Adds separate maximum-size, no-quota growth, registration, and cleanup stress intervals.</param>
    /// <param name="scaling">Measures query/delta growth without the separate ordinary-operation comparison qualification.</param>
    /// <param name="restoreSeed">Optional owned server seed-backup directory with matching schema, codec and population.</param>
    /// <param name="probeUnionAll">Runs only a read-only candidate-union experiment, outside qualification.</param>
    /// <param name="sqlCostThreshold">Owned SQL Server cost threshold; defaults to the engine default of five.</param>
    /// <param name="profileMixed">Captures SQL command/transaction timings under mixed and writer-only diagnostic load; never qualifies performance.</param>
    /// <param name="sqlMaxDop">Owned SQL Server query parallelism cap, zero for the engine default.</param>
    /// <param name="offeredProfile">Replays a recorded ordinary offered load with diagnostic SQL events, outside qualification.</param>
    /// <param name="skipProfileCalibration">Skips saturation history only in an explicitly non-qualifying frozen-load diagnostic.</param>
    /// <param name="offeredProfilePairs">Number of diagnostic alternating pairs, one through five.</param>
    /// <param name="ordinaryComparisonPairs">Three or five alternating full ordinary-operation comparison pairs.</param>
    /// <param name="randomReads">Runs independent random full-content reads without Dimensions.</param>
    /// <param name="updateRecheck">Repeats the original mixed jobs without unrelated qualification phases.</param>
    /// <param name="contentionProfile">Compares matched offered-load cohorts and separately profiles saturated writer stages.</param>
    /// <param name="ownerIndexProbe">Compares temporary owner visibility indexes only in the owned SQL fixture.</param>
    /// <returns>The operation-level failure exit code.</returns>
    /// <example><code>await StorageDimensionsQualification.RunCaseAsync("sqlite", "combined", output: "artifacts/storage-dimensions/smoke");</code></example>
    public static async Task<int> RunCaseAsync(string engine, string feature, int owners = 1000, string output = null, bool smoke = true, string profile = "reference", bool stress = false, bool scaling = false, string restoreSeed = null, bool probeUnionAll = false, int sqlCostThreshold = 5, bool profileMixed = false, int sqlMaxDop = 0, string offeredProfile = null, bool skipProfileCalibration = false, int offeredProfilePairs = 5, int ordinaryComparisonPairs = 3, bool randomReads = false, bool updateRecheck = false, bool contentionProfile = false, bool ownerIndexProbe = false)
    {
        if (engine is not ("sqlserver" or "postgres" or "sqlite") || feature is not ("blob" or "document" or "combined")
            || owners < 400 || profile is not ("reference" or "sparse" or "skew"))
        {
            throw new ArgumentException("Use a declared engine, feature and profile with at least 400 owners.");
        }

        if (ownerIndexProbe && (engine != "sqlserver" || feature != "combined" || profileMixed || randomReads || updateRecheck || contentionProfile || scaling || stress || offeredProfile is not null))
        {
            throw new ArgumentException("Owner-index probes require an isolated combined SQL Server fixture.");
        }

        if (sqlMaxDop is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(sqlMaxDop));
        }

        if (sqlCostThreshold is < 5 or > 32767)
        {
            throw new ArgumentOutOfRangeException(nameof(sqlCostThreshold));
        }

        if (profileMixed && (!smoke || engine != "sqlserver"))
        {
            throw new ArgumentException("Mixed command profiling is a SQL Server diagnostic, not qualification.");
        }

        if (offeredProfile is not null && (!smoke || engine != "sqlserver" || profileMixed))
        {
            throw new ArgumentException("Offered-load replay is an isolated SQL Server diagnostic.");
        }

        if (skipProfileCalibration && offeredProfile is null)
        {
            throw new ArgumentException("Skipping calibration requires an offered-load diagnostic.");
        }

        if (offeredProfilePairs is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(offeredProfilePairs));
        }

        if (ordinaryComparisonPairs is not (3 or 5))
        {
            throw new ArgumentOutOfRangeException(nameof(ordinaryComparisonPairs));
        }

        DimensionQualificationData.Profile = profile;
        output = Path.GetFullPath(output ?? Path.Combine("artifacts", "storage-dimensions", Guid.NewGuid().ToString("N")));
        if (Directory.Exists(output))
        {
            throw new ArgumentException("Output must be a new run-owned directory.");
        }

        Directory.CreateDirectory(output);
        using var timerResolution = new QualificationTimerResolution();
        var timerPrecision = await CaptureTimerPrecisionAsync(timerResolution.Requested);
        var errors = new ConcurrentQueue<string>();
        var limitations = new List<string>
        {
            randomReads ? "Exploratory random full-content reads, without Dimensions or a qualification claim." : updateRecheck ? "Targeted original mixed workload repetition; unrelated qualification phases are skipped." : "This mixed-workload report alone does not complete TEST-015. Separate stress profiles, full matrix coverage, and transaction/log-work qualification remain required.",
            "Latency histograms use 0.01-ms buckets; a percentile above 2000 ms is reported as Infinity, never truncated to a passing value.",
            "Primary latency intervals report whole-interval allocations. Separate diagnostic intervals attribute allocations to individual logical operations."
        };
        if (smoke)
        {
            limitations.Add($"A {owners:N0}-owner short diagnostic run is not volume qualification.");
        }

        if (engine == "sqlite")
        {
            limitations.Add("SQLite decimal ordering is unsupported; decimal range scenarios are excluded explicitly.");
        }

        var preflight = new
        {
            Claim = contentionProfile ? "Matched Dimension contention diagnostic; not qualification." : randomReads ? "Random complete content reads without Dimensions; exploratory warmed scaling study." : updateRecheck ? "Targeted repetition of the original mixed Dimension workload; no full qualification claim." : "Check bounded typed search and single-value deltas under the specified mixed concurrency; do not infer unmeasured capacity.",
            RandomContentReads = randomReads,
            DimensionUpdateRecheck = updateRecheck,
            DimensionContentionProfile = contentionProfile,
            OwnerVisibilityIndexProbe = ownerIndexProbe,
            Engine = engine,
            Feature = feature,
            Profile = profile,
            OwnersPerFeature = owners,
            Smoke = smoke,
            StressEnabled = stress,
            MixedProfile = profileMixed,
            OfferedLoadProfile = offeredProfile,
            ScalingOnly = scaling,
            SqlCostThreshold = sqlCostThreshold,
            SqlMaxDop = sqlMaxDop,
            ReadWorkload = randomReads ? "Random full-content reads without writers or Dimensions" : scaling ? "Selective pages with concurrent dimension writers; broad/count load measured separately" : "Original mixed read/count workload",
            PopulatedOwnersPerFeature = randomReads ? 0 : Enumerable.Range(0, owners).Count(DimensionQualificationData.HasDimensions),
            Seed = DimensionQualificationData.Seed,
            PayloadBytes = randomReads ? (int?)null : DimensionQualificationData.Payload.Length,
            ReferenceScopes = randomReads ? 1 : 10,
            ReferenceNamesPerScope = randomReads ? 0 : 32,
            DimensionsPerPopulatedOwner = randomReads ? 0 : 8,
            ActualScopePopulations = Enumerable.Range(0, owners).GroupBy(owner => randomReads ? 0 : DimensionQualificationData.ScopeNumber(owner))
                .ToDictionary(group => group.Key, group => new { Owners = group.Count(), PopulatedOwners = randomReads ? 0 : group.Count(DimensionQualificationData.HasDimensions) }),
            AdditionalDimensionlessOwnersPerFeature = randomReads ? 0 : 32,
            ImmutableFixtureBulkCopy = restoreSeed is null && (updateRecheck || ownerIndexProbe) && engine == "sqlserver",
            ParentBaselinePopulated = restoreSeed is not null || !(updateRecheck || ownerIndexProbe),
            ActualKindCountsPerFeature = randomReads ? null : DimensionQualificationData.KindCounts(owners),
            ReferenceKindDistribution = randomReads ? "No Dimensions" : "18% Integer, Decimal, Boolean, String, DateTime; 10% Null; deterministic repeating 50-cell schedule",
            Budget = engine == "sqlite" ? "Windows job: 32 GiB and eight logical CPUs shared by coordinator, application and SQLite." : "Windows job: 8 GiB and four logical CPUs shared by coordinator/workload. Database container: 24 GiB, four CPU equivalents.",
            EffectiveApplicationBudget = StorageDimensionsQualificationBudget.CaptureEffectiveBudget(engine, requireMatch: !smoke),
            GCHeapLimitBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            CpuAffinity = OperatingSystem.IsWindows() || OperatingSystem.IsLinux() ? Process.GetCurrentProcess().ProcessorAffinity.ToInt64() : 0,
            LogicalProcessors = Environment.ProcessorCount,
            ProcessId = Environment.ProcessId,
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            ServerGC = System.Runtime.GCSettings.IsServerGC,
            TimerPrecision = timerPrecision,
            OrdinaryComparisonPairs = smoke ? 1 : ordinaryComparisonPairs,
            SqlServerMinBatchSize = engine == "sqlserver" ? (int?)1 : null,
            BinaryFingerprints = new[] { typeof(BlobDimensions).Assembly.Location,
                typeof(Infrastructure.EntityFramework.Storage.EntityFrameworkBlobStoreProvider<>).Assembly.Location,
                typeof(StorageDimensionsQualification).Assembly.Location }
                .ToDictionary(Path.GetFileName, file => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))),
            StartedAt = DateTimeOffset.UtcNow,
            Limitations = limitations
        };
        await WriteAsync(output, "preflight.json", preflight);
        IContainer container = null;
        var commands = new QualificationCommands();
        var mixedProfile = profileMixed || contentionProfile ? new MixedCommandProfile() : null;
        try
        {
            string connection;
            if (engine == "sqlserver")
            {
                var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                    .WithCreateParameterModifier(parameters => { parameters.HostConfig.CpusetCpus = "4-7"; parameters.HostConfig.NanoCPUs = 4_000_000_000; parameters.HostConfig.Memory = 24L * 1024 * 1024 * 1024; }).Build();
                container = sql;
                await sql.StartAsync();
                connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = "dimensions_qualification" }.ConnectionString;
            }
            else if (engine == "postgres")
            {
                var postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("dimensions_qualification")
                    .WithCreateParameterModifier(parameters =>
                    {
                        parameters.HostConfig.CpusetCpus = "4-7";
                        parameters.HostConfig.NanoCPUs = 4_000_000_000;
                        parameters.HostConfig.Memory = 24L * 1024 * 1024 * 1024;
                        parameters.HostConfig.ShmSize = 1024L * 1024 * 1024;
                        // Testcontainers defaults to fsync=off. Qualification requires durable writes.
                        parameters.Cmd = ["postgres", "-c", "fsync=on"];
                    }).Build();
                container = postgres;
                await postgres.StartAsync();
                connection = postgres.GetConnectionString();
            }
            else
            {
                connection = $"Data Source={Path.Combine(output, "owned-fixture.db")};Default Timeout=60;Pooling=True";
            }

            if (container is not null)
            {
                var inspectStart = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                inspectStart.ArgumentList.Add("inspect");
                inspectStart.ArgumentList.Add(container.Id);
                using var inspect = Process.Start(inspectStart) ?? throw new InvalidOperationException("Cannot inspect the owned database resource budget.");
                var inspectionText = await inspect.StandardOutput.ReadToEndAsync();
                await inspect.StandardError.ReadToEndAsync();
                await inspect.WaitForExitAsync();
                if (inspect.ExitCode != 0)
                {
                    throw new InvalidOperationException("Cannot inspect the owned database resource budget.");
                }

                using var inspection = JsonDocument.Parse(inspectionText);
                var hostConfig = inspection.RootElement[0].GetProperty("HostConfig");
                var nanoCpus = hostConfig.GetProperty("NanoCpus").GetInt64();
                var memoryBytes = hostConfig.GetProperty("Memory").GetInt64();
                var cpuSet = hostConfig.GetProperty("CpusetCpus").GetString();
                var sharedMemoryBytes = hostConfig.GetProperty("ShmSize").GetInt64();
                if (nanoCpus != 4_000_000_000 || memoryBytes != 24L * 1024 * 1024 * 1024 || cpuSet != "4-7")
                {
                    throw new InvalidOperationException("Owned database limits differ from the declared budget.");
                }

                if (engine == "postgres" && sharedMemoryBytes != 1024L * 1024 * 1024)
                {
                    throw new InvalidOperationException("Owned PostgreSQL shared-memory limit differs from the declared budget.");
                }
                // Inspect output also contains generated test credentials; persist only these budget fields.
                await WriteAsync(output, "database-budget.json", new
                {
                    ContainerId = container.Id,
                    NanoCpus = nanoCpus,
                    CpuSet = cpuSet,
                    MemoryBytes = memoryBytes,
                    SharedMemoryBytes = sharedMemoryBytes
                });
            }

            var baselineConnection = engine switch
            {
                "sqlserver" => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection) { InitialCatalog = "dimensions_parent_baseline" }.ConnectionString,
                "postgres" => new Npgsql.NpgsqlConnectionStringBuilder(connection) { Database = "dimensions_parent_baseline" }.ConnectionString,
                _ => $"Data Source={Path.Combine(output, "owned-parent-baseline.db")};Default Timeout=60;Pooling=True"
            };

            await using var services = new ServiceCollection().AddDbContext<DimensionsQualificationContext>(options =>
            {
                if (engine == "sqlserver")
                {
                    options.UseSqlServer(connection, sql => sql.MinBatchSize(1));
                }
                else if (engine == "postgres")
                {
                    options.UseNpgsql(connection);
                }
                else
                {
                    options.UseSqlite(connection);
                }

                options.AddInterceptors(commands);
                if (mixedProfile is not null)
                {
                    options.AddInterceptors(mixedProfile.Commands, mixedProfile.Transactions);
                }
            }).AddDbContext<DimensionsParentOnlyContext>(options =>
            {
                if (engine == "sqlserver")
                {
                    options.UseSqlServer(baselineConnection, sql => sql.MinBatchSize(1));
                }
                else if (engine == "postgres")
                {
                    options.UseNpgsql(baselineConnection);
                }
                else
                {
                    options.UseSqlite(baselineConnection);
                }

                options.AddInterceptors(commands);
                if (mixedProfile is not null)
                {
                    options.AddInterceptors(mixedProfile.Commands, mixedProfile.Transactions);
                }
            }).BuildServiceProvider();
            await using (var scope = services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
                await context.Database.EnsureCreatedAsync();
                if (engine == "sqlserver" && sqlCostThreshold != 5)
                {
                    using var configure = context.Database.GetDbConnection().CreateCommand();
                    await context.Database.OpenConnectionAsync();
                    configure.CommandText = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'cost threshold for parallelism', @threshold; RECONFIGURE;";
                    var parameter = configure.CreateParameter();
                    parameter.ParameterName = "@threshold";
                    parameter.Value = sqlCostThreshold;
                    configure.Parameters.Add(parameter);
                    await configure.ExecuteNonQueryAsync();
                }

                if (engine == "sqlserver" && sqlMaxDop != 0)
                {
                    await ConfigureSqlMaxDopAsync(scopes: services.GetRequiredService<IServiceScopeFactory>(), sqlMaxDop);
                }

                if (engine == "sqlite")
                {
                    await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                }

                await File.WriteAllTextAsync(Path.Combine(output, "schema.sql"), context.Database.GenerateCreateScript());
                var baseline = scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>();
                await baseline.Database.EnsureCreatedAsync();
                if (engine == "sqlite")
                {
                    await baseline.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                }

                await File.WriteAllTextAsync(Path.Combine(output, "parent-baseline-schema.sql"), baseline.Database.GenerateCreateScript());
                await context.Database.OpenConnectionAsync();
                using var settings = context.Database.GetDbConnection().CreateCommand();
                settings.CommandText = engine switch
                {
                    "sqlserver" => "SELECT @@VERSION AS Version, CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(),'Collation')) AS Collation; SELECT cpu_count AS CpuCount, (SELECT COUNT(*) FROM sys.dm_os_schedulers WHERE status='VISIBLE ONLINE') AS VisibleSchedulers FROM sys.dm_os_sys_info; SELECT name,value_in_use FROM sys.configurations WHERE name IN ('max server memory (MB)','max degree of parallelism','cost threshold for parallelism')",
                    "postgres" => "SELECT version() AS version, current_setting('server_encoding') AS encoding, (SELECT datcollate FROM pg_database WHERE datname=current_database()) AS collation, current_setting('shared_buffers') AS shared_buffers, current_setting('work_mem') AS work_mem, current_setting('max_connections') AS max_connections, current_setting('fsync') AS fsync, current_setting('synchronous_commit') AS synchronous_commit, current_setting('full_page_writes') AS full_page_writes, current_setting('dynamic_shared_memory_type') AS dynamic_shared_memory_type, current_setting('max_parallel_workers') AS max_parallel_workers, current_setting('max_parallel_workers_per_gather') AS max_parallel_workers_per_gather",
                    _ => "SELECT sqlite_version() AS version; PRAGMA journal_mode; PRAGMA busy_timeout; PRAGMA cache_size; PRAGMA compile_options"
                };
                await using var settingsReader = await settings.ExecuteReaderAsync();
                var settingRows = new List<Dictionary<string, object>>();
                do
                {
                    while (await settingsReader.ReadAsync())
                    {
                        settingRows.Add(Enumerable.Range(0, settingsReader.FieldCount).ToDictionary(settingsReader.GetName, index => settingsReader.GetValue(index)));
                    }
                } while (await settingsReader.NextResultAsync());
                await WriteAsync(output, "database-settings.json", settingRows);
                if (engine == "sqlserver" && Convert.ToInt32(settingRows.Single(row => row.ContainsKey("VisibleSchedulers"))["VisibleSchedulers"]) != 4)
                {
                    throw new InvalidOperationException("SQL Server must see four online schedulers inside the database CPU budget before seeding or measurement.");
                }

                if (engine == "postgres" && settingRows.Any(row => row["fsync"].ToString() != "on" || row["synchronous_commit"].ToString() != "on" || row["full_page_writes"].ToString() != "on"))
                {
                    throw new InvalidOperationException("PostgreSQL durability settings must be enabled before seeding or measurement.");
                }
            }

            var scopes = services.GetRequiredService<IServiceScopeFactory>();
            if (randomReads)
            {
                return await RunRandomReadWorkloadAsync(scopes, engine, feature, owners, output, smoke, commands, container);
            }

            if (offeredProfile is not null)
            {
                await StartSqlSchedulingEventsAsync(scopes, output);
            }

            var blobs = feature != "document" ? new BlobDimensionsQualification(scopes) : null;
            var documents = feature != "blob" ? new DocumentDimensionsQualification(scopes) : null;
            var seedTimer = Stopwatch.StartNew();
            Console.WriteLine($"[{engine}/{feature}/{owners}] seeding owned fixture");
            if (restoreSeed is not null)
            {
                if (engine == "sqlserver")
                {
                    await RestoreSqlServerSeedAsync(connection, container.Id, restoreSeed, output, owners, feature, profile);
                }
                else if (engine == "postgres")
                {
                    await RestorePostgresSeedAsync(connection, container.Id, restoreSeed, output, owners, feature, profile);
                }
                else
                {
                    throw new ArgumentException("Seed restoration supports owned server fixtures only.");
                }

                await VerifyRestoredSnapshotsAsync(scopes, owners, feature);
            }
            else
            {
                if (blobs is not null)
                {
                    await blobs.SeedAsync(owners, (updateRecheck || ownerIndexProbe) && engine == "sqlserver");
                }

                if (documents is not null)
                {
                    await documents.SeedAsync(owners, (updateRecheck || ownerIndexProbe) && engine == "sqlserver");
                }
                // Isolated update/index diagnostics never execute parent-only comparisons.
                if (!updateRecheck && !ownerIndexProbe)
                {
                    if (blobs is not null)
                    {
                        await blobs.SeedBaselineAsync(owners);
                    }

                    if (documents is not null)
                    {
                        await documents.SeedBaselineAsync(owners);
                    }
                }

                if (blobs is not null)
                {
                    await blobs.SeedDimensionlessAsync();
                }

                if (documents is not null)
                {
                    await documents.SeedDimensionlessAsync();
                }

                if ((updateRecheck || ownerIndexProbe) && engine == "sqlserver")
                {
                    await VerifyRestoredSnapshotsAsync(scopes, owners, feature);
                }
            }

            if (engine == "sqlserver" && !profileMixed && offeredProfile is null)
            {
                await ProvisionSqlLogsAsync(scopes, output);
            }

            await RefreshStatisticsAsync(scopes, engine);
            await WriteAsync(output, "statistics-refresh.json", new
            {
                Engine = engine,
                Command = engine == "sqlserver" ? "EXEC sys.sp_updatestats" : "ANALYZE",
                CapturedAt = DateTimeOffset.UtcNow
            });
            Console.WriteLine($"Fixture verified in {seedTimer.Elapsed.TotalSeconds:F1}s");
            var jobs = new List<DimensionQualificationJob>();
            if (blobs is not null)
            {
                jobs.AddRange(await blobs.CreateJobsAsync(owners, engine));
            }

            if (documents is not null)
            {
                jobs.AddRange(await documents.CreateJobsAsync(owners, engine));
            }

            object storage;
            try
            {
                storage = new
                {
                    Current = await CaptureStorageAsync(scopes, engine),
                    ParentBaseline = await CaptureStorageAsync(scopes, engine, parentOnly: true)
                };
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("dbstat", StringComparison.Ordinal))
            {
                storage = new
                {
                    Current = await CaptureSqliteFootprintAsync(Path.Combine(output, "owned-fixture.db")),
                    ParentBaseline = await CaptureSqliteFootprintAsync(Path.Combine(output, "owned-parent-baseline.db"))
                };
            }

            await WriteAsync(output, "storage.json", storage);
            if (engine == "sqlserver" && !smoke && restoreSeed is null && !contentionProfile && !ownerIndexProbe && !updateRecheck)
            {
                await SaveSqlServerSeedAsync(connection, container.Id, output, owners, feature, profile);
            }

            if (engine == "postgres" && !smoke && restoreSeed is null && !contentionProfile && !ownerIndexProbe && !updateRecheck)
            {
                await SavePostgresSeedAsync(connection, container.Id, output, owners, feature, profile);
            }

            if (container is PostgreSqlContainer coldPostgres)
            {
                Npgsql.NpgsqlConnection.ClearAllPools();
                await coldPostgres.StopAsync();
                await coldPostgres.StartAsync();
                // Docker can assign a different ephemeral host port after restart. Context factories capture these variables.
                connection = coldPostgres.GetConnectionString();
                baselineConnection = new Npgsql.NpgsqlConnectionStringBuilder(connection) { Database = "dimensions_parent_baseline" }.ConnectionString;
            }

            if (container is not null)
            {
                await CaptureColdDatabaseReadsAsync(scopes, engine, output, jobs);
            }

            commands.Enabled = true;
            // Profiling and verification are separate from the reported steady-state latency interval.
            foreach (var job in jobs)
            {
                commands.CaseName = job.Name;
                await job.Execute(0);
            }

            await WriteAsync(output, "profile-commands.json", commands.Text.ToArray());
            commands.Enabled = false;
            await CapturePlansAsync(scopes, engine, output, commands.Queries);
            if (probeUnionAll)
            {
                if (engine != "sqlserver")
                {
                    throw new ArgumentException("The union experiment targets SQL Server only.");
                }

                await ProbeUnionAllAsync(scopes, output, commands.Queries);
                return 0;
            }

            if (ownerIndexProbe)
            {
                return await ProbeOwnerVisibilityIndexAsync(scopes, output, commands.Queries);
            }

            if (contentionProfile)
            {
                return await RunDimensionContentionProfileAsync(scopes, engine, jobs, output, smoke, commands, mixedProfile, container);
            }

            if (updateRecheck)
            {
                return await RunDimensionUpdateRecheckAsync(scopes, engine, jobs, output, smoke, commands);
            }

            if (profileMixed)
            {
                return await ProfileMixedAsync(scopes, engine, jobs, mixedProfile, output, commands.Queries);
            }

            if (offeredProfile is not null)
            {
                return await ProfileOfferedAsync(blobs, documents, scopes, output, offeredProfile, feature, owners, skipProfileCalibration, offeredProfilePairs);
            }

            var comparisons = new List<object>();
            var allocationProfiles = new List<object>();
            if (!scaling)
            {
                var offeredLoad = await CalibrateOrdinaryLoadAsync(blobs, documents, engine, output, smoke, errors);
                for (var comparison = 0; comparison < (smoke ? 1 : ordinaryComparisonPairs); comparison++)
                {
                    // Alternate order to avoid assigning a warm-up/aging advantage to either model.
                    foreach (var parentOnly in comparison % 2 == 0 ? new[] { true, false } : new[] { false, true })
                    {
                        var baselineJobs = new List<DimensionQualificationJob>();
                        if (blobs is not null)
                        {
                            baselineJobs.AddRange(blobs.BaselineJobs(parentOnly));
                        }

                        if (documents is not null)
                        {
                            baselineJobs.AddRange(documents.BaselineJobs(parentOnly));
                        }

                        commands.Text.Clear();
                        commands.Enabled = true;
                        foreach (var job in baselineJobs)
                        {
                            await job.Execute(0);
                        }

                        commands.Enabled = false;
                        if (commands.Text.Any(command => JsonSerializer.Serialize(command).Contains("__Storage_BlobDimension", StringComparison.Ordinal)
                            || JsonSerializer.Serialize(command).Contains("__Storage_DocumentDimension", StringComparison.Ordinal)))
                        {
                            throw new InvalidOperationException("An ordinary baseline operation accessed a dimension table.");
                        }

                        await LoadOfferedAsync(baselineJobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 15), errors, offeredLoad, false);
                        var workBefore = await CaptureDatabaseWorkAsync(scopes, engine, parentOnly);
                        var missedRequests = new ConcurrentQueue<object>();
                        var timerBefore = await CaptureTimerPrecisionAsync(timerResolution.Requested);
                        var gcBefore = CaptureGCPauses();
                        var allocationStart = GC.GetTotalAllocatedBytes(precise: true);
                        var commandsBefore = commands.Capture();
                        var baselineTimer = Stopwatch.StartNew();
                        var baselineCases = await LoadOfferedAsync(baselineJobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 120), errors, offeredLoad, true, missedRequests);
                        baselineTimer.Stop();
                        var gcAfter = CaptureGCPauses();
                        var commandWork = commands.Capture().Subtract(commandsBefore);
                        var allocationDelta = GC.GetTotalAllocatedBytes(precise: true) - allocationStart;
                        var timerAfter = await CaptureTimerPrecisionAsync(timerResolution.Requested);
                        var workAfter = await CaptureDatabaseWorkAsync(scopes, engine, parentOnly);
                        comparisons.Add(new
                        {
                            TimerPrecisionBefore = timerBefore,
                            TimerPrecisionAfter = timerAfter,
                            GCPausesBefore = gcBefore,
                            GCPausesAfter = gcAfter,
                            Pair = comparison + 1,
                            ParentOnly = parentOnly,
                            OfferedLoad = offeredLoad,
                            Cases = baselineCases,
                            MissedRequestDetails = missedRequests.ToArray(),
                            ScheduledSeconds = smoke ? 3 : 120,
                            DurationIncludingDrainSeconds = baselineTimer.Elapsed.TotalSeconds,
                            AllocatedBytesPerOperation = allocationDelta / (double)Math.Max(1, baselineCases.Sum(item => item.Operations)),
                            DatabaseWorkBefore = workBefore,
                            DatabaseWorkAfter = workAfter,
                            CommandWork = commandWork
                        });
                        await WriteAsync(output, $"baseline-{comparison + 1}-{(parentOnly ? "parent" : "dimensions")}.json", comparisons[^1]);
                        Console.WriteLine($"Baseline pair {comparison + 1}/{(smoke ? 1 : ordinaryComparisonPairs)} {(parentOnly ? "parent" : "dimensions")}: operations={baselineCases.Sum(item => item.Operations)}, failures={baselineCases.Sum(item => item.Failures)}");
                    }
                }

                await StorageDimensionsAllocationCounter.VerifyAsync();
                for (var attributionRun = 0; attributionRun < (smoke ? 1 : 3); attributionRun++)
                {
                    foreach (var parentOnly in attributionRun % 2 == 0 ? new[] { true, false } : new[] { false, true })
                    {
                        var allocationJobs = new List<DimensionQualificationJob>();
                        if (blobs is not null)
                        {
                            allocationJobs.AddRange(blobs.BaselineJobs(parentOnly));
                        }

                        if (documents is not null)
                        {
                            allocationJobs.AddRange(documents.BaselineJobs(parentOnly));
                        }

                        var allocationCases = await LoadAsync(allocationJobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 10), errors, true, measureAllocations: true);
                        var total = allocationCases[0].IntervalAllocatedBytes;
                        var covered = allocationCases.Sum(item => item.AllocatedBytes) / (double)Math.Max(1, total);
                        if (!smoke && (covered < 0.98 || covered > 1.01))
                        {
                            errors.Enqueue("Per-operation allocation attribution coverage is incomplete.");
                        }

                        allocationProfiles.Add(new
                        {
                            Run = attributionRun + 1,
                            ParentOnly = parentOnly,
                            AttributionCoverage = covered,
                            Note = "Separate logical-flow allocation profile under the same configured concurrency; its timings are excluded from latency qualification.",
                            Cases = allocationCases.Select(item => new { item.Name, item.Operations, item.Failures, item.AllocatedBytes, item.AllocatedBytesPerOperation }).ToArray()
                        });
                        await WriteAsync(output, $"allocations-{attributionRun + 1}-{(parentOnly ? "parent" : "dimensions")}.json", allocationProfiles[^1]);
                    }
                }

            }

            var selectiveJobs = scaling ? jobs.Where(job => job.SelectiveTarget).ToArray() : jobs.ToArray();
            var results = new List<object>();
            for (var run = 0; run < (smoke ? 1 : 3); run++)
            {
                blobs?.ClearCaches();
                documents?.ClearCaches();
                var cold = Stopwatch.StartNew();
                foreach (var job in selectiveJobs.Where(job => !job.Writer))
                {
                    await job.Execute(0);
                }

                var coldMilliseconds = cold.Elapsed.TotalMilliseconds;
                await LoadAsync(selectiveJobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 15), errors, record: false);
                var workBefore = await CaptureDatabaseWorkAsync(scopes, engine);
                var gcBefore = CaptureGCPauses();
                var allocated = GC.GetTotalAllocatedBytes(precise: true);
                var elapsed = Stopwatch.StartNew();
                var commandsBefore = commands.Capture();
                var cases = await LoadAsync(selectiveJobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 120), errors, record: true);
                var commandWork = commands.Capture().Subtract(commandsBefore);
                elapsed.Stop();
                var gcAfter = CaptureGCPauses();
                var allocationDelta = GC.GetTotalAllocatedBytes(precise: true) - allocated;
                var workAfter = await CaptureDatabaseWorkAsync(scopes, engine);
                results.Add(new
                {
                    GCPausesBefore = gcBefore,
                    GCPausesAfter = gcAfter,
                    Run = run + 1,
                    DurationSeconds = elapsed.Elapsed.TotalSeconds,
                    ColdDictionarySequenceMilliseconds = coldMilliseconds,
                    AllocatedBytes = allocationDelta,
                    DatabaseWorkBefore = workBefore,
                    DatabaseWorkAfter = workAfter,
                    CommandWork = commandWork,
                    WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
                    ApplicationBudgetUsage = StorageDimensionsQualificationBudget.CaptureEffectiveBudget(engine, requireMatch: !smoke),
                    Throughput = cases.Sum(item => item.Operations) / elapsed.Elapsed.TotalSeconds,
                    Cases = cases
                });
                await WriteAsync(output, $"run-{run + 1}.json", results[^1]);
                await WriteAsync(output, $"counters-{run + 1}.json", new
                {
                    Blob = blobs is null ? null : await blobs.CaptureCountersAsync(),
                    Document = documents is null ? null : await documents.CaptureCountersAsync()
                });
                Console.WriteLine($"Measured run {run + 1} finished, failures={cases.Sum(item => item.Failures)}");
            }

            var broadResults = new List<object>();
            var broadJobs = jobs.Where(job => !job.SelectiveTarget || job.Writer).ToArray();
            if (scaling && broadJobs.Any(job => !job.Writer))
            {
                for (var run = 0; run < (smoke ? 1 : 3); run++)
                {
                    await LoadAsync(broadJobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 15), errors, false);
                    var workBefore = await CaptureDatabaseWorkAsync(scopes, engine);
                    var commandsBefore = commands.Capture();
                    var elapsed = Stopwatch.StartNew();
                    var cases = await LoadAsync(broadJobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 120), errors, true);
                    elapsed.Stop();
                    broadResults.Add(new
                    {
                        Run = run + 1,
                        DurationSeconds = elapsed.Elapsed.TotalSeconds,
                        Throughput = cases.Sum(item => item.Operations) / elapsed.Elapsed.TotalSeconds,
                        DatabaseWorkBefore = workBefore,
                        DatabaseWorkAfter = await CaptureDatabaseWorkAsync(scopes, engine),
                        CommandWork = commands.Capture().Subtract(commandsBefore),
                        Cases = cases,
                        Note = "Broad/count workload with concurrent dimension writers; no selective-latency promise. Failures still invalidate operation checks."
                    });
                    await WriteAsync(output, $"broad-run-{run + 1}.json", broadResults[^1]);
                    Console.WriteLine($"Broad/count run {run + 1} finished, failures={cases.Sum(item => item.Failures)}");
                }
            }

            var stressResults = new List<object>();
            object stressCleanup = null;
            if (stress)
            {
                var stressJobs = new List<DimensionQualificationJob>();
                Console.WriteLine("Preparing maximum-size and 10,240-distinct-name stress fixtures");
                if (blobs is not null)
                {
                    stressJobs.AddRange(await blobs.CreateStressJobsAsync());
                }

                if (documents is not null)
                {
                    stressJobs.AddRange(await documents.CreateStressJobsAsync());
                }

                await RefreshStatisticsAsync(scopes, engine);
                await WriteAsync(output, "stress-storage-before.json", await CaptureCurrentStorageAsync(scopes, engine, output));
                commands.Queries.Clear();
                commands.Text.Clear();
                commands.Enabled = true;
                foreach (var job in stressJobs)
                {
                    commands.CaseName = job.Name;
                    await job.Execute(0);
                }

                commands.Enabled = false;
                var stressProfile = Path.Combine(output, "stress-profile");
                Directory.CreateDirectory(stressProfile);
                await WriteAsync(stressProfile, "commands.json", commands.Text.ToArray());
                await CapturePlansAsync(scopes, engine, stressProfile, commands.Queries);
                for (var run = 0; run < (smoke ? 1 : 3); run++)
                {
                    await LoadAsync(stressJobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 15), errors, false);
                    var workBefore = await CaptureDatabaseWorkAsync(scopes, engine);
                    var commandsBefore = commands.Capture();
                    var cases = await LoadAsync(stressJobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 120), errors, true);
                    var commandWork = commands.Capture().Subtract(commandsBefore);
                    var workAfter = await CaptureDatabaseWorkAsync(scopes, engine);
                    stressResults.Add(new
                    {
                        Run = run + 1,
                        Cases = cases,
                        CommandWork = commandWork,
                        DatabaseWorkBefore = workBefore,
                        DatabaseWorkAfter = workAfter,
                        WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
                        Blob = blobs is null ? null : await blobs.CaptureCountersAsync(),
                        Document = documents is null ? null : await documents.CaptureCountersAsync(),
                        Note = "Separate stress workload; these cases have no 250-ms promise."
                    });
                    await WriteAsync(output, $"stress-{run + 1}.json", stressResults[^1]);
                    Console.WriteLine($"Stress run {run + 1} finished, failures={cases.Sum(item => item.Failures)}");
                }

                stressCleanup = new
                {
                    Blob = blobs is null ? null : await blobs.CleanupStressAsync(),
                    Document = documents is null ? null : await documents.CleanupStressAsync()
                };
                await WriteAsync(output, "stress-cleanup.json", stressCleanup);
                await WriteAsync(output, "stress-storage-after.json", await CaptureCurrentStorageAsync(scopes, engine, output));
            }

            var valueCount = Enumerable.Range(0, owners).LongCount(DimensionQualificationData.HasDimensions) * 8 * (feature == "combined" ? 2 : 1);
            var assessment = AssessMeasuredGates(results, comparisons, allocationProfiles, storage, valueCount, profile, smoke, scaling);
            await WriteAsync(output, "assessment.json", assessment.Report);
            await WriteAsync(output, "qualification.json", new
            {
                Preflight = preflight,
                Results = results,
                BroadResults = broadResults,
                ParentComparisons = comparisons,
                AllocationProfiles = allocationProfiles,
                StressResults = stressResults,
                StressCleanup = stressCleanup,
                Storage = storage,
                Errors = errors.ToArray(),
                OperationChecksPassed = errors.IsEmpty,
                ScalingChecksPassed = scaling ? assessment.Passed && errors.IsEmpty : (bool?)null,
                MeasuredGateSubsetPassed = smoke || scaling || profile != "reference" ? (bool?)null : assessment.Passed && errors.IsEmpty,
                QualificationComplete = false,
                Limitations = limitations
            });
            return errors.IsEmpty && assessment.Passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            await WriteAsync(output, "failure.json", new
            {
                ExceptionType = exception.GetType().FullName,
                exception.Message,
                exception.StackTrace,
                Errors = errors.ToArray(),
                Limitations = limitations
            });
            Console.Error.WriteLine($"Qualification failed: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
        finally
        {
            if (container is not null)
            {
                try
                {
                    var logs = await container.GetLogsAsync();
                    await File.WriteAllTextAsync(Path.Combine(output, "database-stdout.log"), logs.Stdout);
                    await File.WriteAllTextAsync(Path.Combine(output, "database-stderr.log"), logs.Stderr);
                }
                catch (Exception exception) { Console.Error.WriteLine($"Cannot preserve owned database logs: {exception.Message}"); }

                await container.DisposeAsync();
            }
            // SQLite files remain as evidence. No existing catalog or external path is deleted.
        }
    }

    internal static T Require<T>(Result<T> result) => result.IsSuccess ? result.Value : throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error =>
        error is DocumentStoreProviderError { InnerException: not null } provider
            ? $"{error.Message} {provider.InnerException.GetType().Name}: {provider.InnerException.GetFullMessage()}"
            : error.Message)));

    private static async Task<IReadOnlyList<DimensionQualificationCase>> LoadAsync(IReadOnlyList<DimensionQualificationJob> jobs, string engine, TimeSpan duration, ConcurrentQueue<string> errors, bool record, bool measureAllocations = false, int readerCount = 16)
    {
        var cases = jobs.Select(job => new DimensionQualificationCase(job.Name, job.ExpectedMatches, job.SelectiveTarget)).ToArray();
        var readers = jobs.Select((job, index) => (job, index)).Where(pair => !pair.job.Writer).ToArray();
        var writers = jobs.Select((job, index) => (job, index)).Where(pair => pair.job.Writer).ToArray();
        var writerCount = writers.Length == 0 ? 0 : engine == "sqlite" ? 1 : 4;
        var workerCount = readerCount + writerCount;
        var ready = 0;
        long end = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var intervalAllocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        ThreadPool.GetMinThreads(out var minimumWorkers, out var minimumIo);
        ThreadPool.SetMinThreads(Math.Max(minimumWorkers, workerCount + 4), minimumIo);
        async Task Worker(int worker, bool writer)
        {
            var selected = writer ? writers : readers;
            var iteration = worker;
            if (Interlocked.Increment(ref ready) == workerCount)
            {
                end = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
                start.SetResult();
            }

            await start.Task;
            while (Stopwatch.GetTimestamp() < end)
            {
                var (job, index) = selected[iteration++ % selected.Length];
                var started = Stopwatch.GetTimestamp();
                var failed = false;
                var rows = 0;
                using var allocation = measureAllocations ? new StorageDimensionsAllocationCounter() : null;
                try
                {
                    rows = await job.Execute(worker);
                }
                catch (Exception exception)
                {
                    failed = true;
                    if (errors.Count < 32)
                    {
                        errors.Enqueue($"{job.Name}: {exception.GetType().Name}: {exception.Message}");
                    }
                }

                allocation?.Dispose();
                if (record)
                {
                    cases[index].Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, rows, failed);
                    if (allocation is not null)
                    {
                        cases[index].RecordAllocation(allocation.Bytes);
                    }
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, readerCount).Select(worker => Task.Run(() => Worker(worker, false)))
            .Concat(Enumerable.Range(0, writerCount).Select(worker => Task.Run(() => Worker(worker, true)))));
        cases[0].IntervalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - intervalAllocatedBefore;
        if (record && cases.Any(item => item.Operations == 0))
        {
            errors.Enqueue("Some declared operation cases received no measured work; this interval is invalid.");
        }

        return cases;
    }

    private static async Task<object> CaptureStorageAsync(IServiceScopeFactory scopes, string engine, bool parentOnly = false)
    {
        await using var scope = scopes.CreateAsyncScope();
        DbContext context = parentOnly ? scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>() : scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        var sql = engine switch
        {
            "sqlserver" => "SELECT t.name AS TableName, i.name AS IndexName, SUM(p.used_page_count)*8192 AS Bytes FROM sys.dm_db_partition_stats p JOIN sys.tables t ON t.object_id=p.object_id JOIN sys.indexes i ON i.object_id=p.object_id AND i.index_id=p.index_id WHERE t.name LIKE '__Storage_%' GROUP BY t.name,i.name ORDER BY t.name,i.name",
            "postgres" => "SELECT c.relname AS name, CASE WHEN c.relkind='r' THEN pg_table_size(c.oid) ELSE pg_relation_size(c.oid) END AS bytes FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN ('r','i','S') AND (c.relname LIKE '__Storage_%' OR c.relname LIKE 'IX_%' OR c.relname LIKE 'PK_%') ORDER BY c.relname",
            _ => "SELECT name, SUM(pgsize) AS bytes FROM dbstat GROUP BY name ORDER BY name"
        };
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var result = new List<Dictionary<string, object>>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                result.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, index => reader.GetValue(index)));
            }
        }

        var usedBytes = result.Sum(row => Convert.ToInt64(row.TryGetValue("Bytes", out var bytes) ? bytes : row["bytes"], System.Globalization.CultureInfo.InvariantCulture));
        if (engine == "postgres")
        {
            // The aggregate includes TOAST and all relation forks; detail rows retain individual index sizes.
            command.CommandText = "SELECT COALESCE(SUM(CASE WHEN c.relkind='r' THEN pg_total_relation_size(c.oid) ELSE pg_relation_size(c.oid) END),0) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN ('r','S') AND c.relname LIKE '__Storage_%'";
            usedBytes = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }

        return new
        {
            Relations = result,
            UsedBytes = usedBytes
        };
    }

    private static string Argument(string[] args, string name, string fallback)
    {
        var index = Array.IndexOf(args, name);
        return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing {name} value.");
    }

    private static Task WriteAsync(string output, string name, object value) => File.WriteAllTextAsync(Path.Combine(output, name), JsonSerializer.Serialize(value, JsonOptions));

    private sealed class QualificationCommands : DbCommandInterceptor
    {
        private long readers;
        private long nonQueries;
        private long parameters;
        private long affectedRows;

        public QualificationCommandCounts Capture() => new(Interlocked.Read(ref this.readers), Interlocked.Read(ref this.nonQueries), Interlocked.Read(ref this.parameters), Interlocked.Read(ref this.affectedRows));

        public bool Enabled
        {
            get; set;
        }

        public string CaseName
        {
            get; set;
        }

        public ConcurrentQueue<object> Text { get; } = new();

        public ConcurrentQueue<QualificationQuery> Queries { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.readers);
            Interlocked.Add(ref this.parameters, command.Parameters.Count);
            if (this.Enabled && this.Text.Count < 256)
            {
                this.Text.Enqueue(new
                {
                    command.CommandText,
                    Parameters = command.Parameters.Cast<DbParameter>().Select(parameter => new { parameter.ParameterName, parameter.Value, parameter.DbType }).ToArray()
                });
            }

            if (this.Enabled && this.Queries.Count < 128 && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && (command.CommandText.Contains("__Storage_BlobDimensions", StringComparison.Ordinal) || command.CommandText.Contains("__Storage_DocumentDimensions", StringComparison.Ordinal)))
            {
                this.Queries.Enqueue(new(this.CaseName, command.CommandText, command.Parameters.Cast<DbParameter>().Select(parameter =>
                    new QualificationParameter(parameter.ParameterName, parameter.DbType, parameter.Value, parameter.Size, parameter.Precision, parameter.Scale,
                        parameter is ICloneable cloneable ? (DbParameter)cloneable.Clone() : null)).ToArray()));
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.nonQueries);
            Interlocked.Add(ref this.parameters, command.Parameters.Count);
            if (this.Enabled && this.Text.Count < 256)
            {
                this.Text.Enqueue(new
                {
                    command.CommandText,
                    Parameters = command.Parameters.Cast<DbParameter>().Select(parameter => new { parameter.ParameterName, parameter.Value, parameter.DbType }).ToArray()
                });
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (result > 0)
            {
                Interlocked.Add(ref this.affectedRows, result);
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed record QualificationCommandCounts(long ReaderBatches, long NonQueryBatches, long Parameters, long AffectedRowsFromNonQuery)
    {
        public QualificationCommandCounts Subtract(QualificationCommandCounts before) => new(this.ReaderBatches - before.ReaderBatches,
            this.NonQueryBatches - before.NonQueryBatches, this.Parameters - before.Parameters, this.AffectedRowsFromNonQuery - before.AffectedRowsFromNonQuery);
    }
}

internal sealed record DimensionQualificationJob(string Name, bool Writer, int ExpectedMatches, bool SelectiveTarget, Func<int, Task<int>> Execute);
