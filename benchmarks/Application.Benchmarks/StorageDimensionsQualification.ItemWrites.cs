// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

public static partial class StorageDimensionsQualification
{
    private static async Task<int> RunItemWritesAsync(string engine, string output, bool smoke, bool bulkOnly, int? selectedOwners)
    {
        if (output is null)
        {
            throw new ArgumentException("Item-write measurements require a new output directory.");
        }

        if (selectedOwners.HasValue && selectedOwners is not (10000 or 100000 or 1000000))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedOwners));
        }

        int[] populations = smoke ? [1000] : selectedOwners.HasValue ? [selectedOwners.Value] : [10000, 100000, 1000000];
        output = Path.GetFullPath(output);
        if (Directory.Exists(output))
        {
            throw new ArgumentException("Output must be new.");
        }

        Directory.CreateDirectory(output);
        using var timerResolution = new QualificationTimerResolution();
        await WriteAsync(output, "definition.json", new
        {
            Engine = engine,
            Smoke = smoke,
            PayloadBytes = 1024,
            Dimensions = new[] { 0, 3 },
            Types = new[] { "Integer", "Boolean", "String" },
            Coordinators = 1,
            MaximumBlobUploadWorkers = 4,
            BulkUploadsOnly = bulkOnly,
            Owners = populations,
            Repetitions = smoke ? 1 : 5,
            SingleSamplesPerRepetition = smoke ? 3 : 100,
            BatchSamplesPerRepetition = smoke ? 2 : 10,
            BlobBatch = "100 sequential UploadAsync calls and public UploadManyAsync with four workers",
            DocumentBatch = "UpsertManyAsync with 100 writes",
            Boundary = "EF provider through acknowledged content commit; payload preparation, verification and add cleanup outside timing. No HTTP, transforms or conditional ETag inputs.",
            Fixture = "Growing owned collection; all records have 0 or 3 Dimensions at each point. Names registered before timing. Add operations start at exactly N records and are removed after verification.",
            Claim = "Exploratory warmed write scaling, not full qualification or an SLA.",
            EffectiveBudget = StorageDimensionsQualificationBudget.CaptureEffectiveBudget(engine, requireMatch: !smoke),
            TimerPrecision = await CaptureTimerPrecisionAsync(timerResolution.Requested),
            StartedAtUtc = DateTimeOffset.UtcNow
        });
        IContainer container = null;
        try
        {
            string connection;
            if (engine == "sqlserver")
            {
                var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithCreateParameterModifier(parameters =>
                {
                    parameters.HostConfig.CpusetCpus = "4-7";
                    parameters.HostConfig.NanoCPUs = 4_000_000_000;
                    parameters.HostConfig.Memory = 24L * 1024 * 1024 * 1024;
                }).Build();
                container = sql;
                await sql.StartAsync();
                connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = "item_write_scaling" }.ConnectionString;
            }
            else
            {
                var postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("item_write_scaling").WithCreateParameterModifier(parameters =>
                {
                    parameters.HostConfig.CpusetCpus = "4-7";
                    parameters.HostConfig.NanoCPUs = 4_000_000_000;
                    parameters.HostConfig.Memory = 24L * 1024 * 1024 * 1024;
                    parameters.HostConfig.ShmSize = 1024L * 1024 * 1024;
                    parameters.Cmd = ["postgres", "-c", "fsync=on"];
                }).Build();
                container = postgres;
                await postgres.StartAsync();
                connection = postgres.GetConnectionString();
            }

            var inspection = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            inspection.ArgumentList.Add("inspect");
            inspection.ArgumentList.Add(container.Id);
            using var inspect = Process.Start(inspection) ?? throw new InvalidOperationException("Cannot inspect owned budget.");
            var text = await inspect.StandardOutput.ReadToEndAsync();
            await inspect.StandardError.ReadToEndAsync();
            await inspect.WaitForExitAsync();
            using var json = JsonDocument.Parse(text);
            var config = json.RootElement[0].GetProperty("HostConfig");
            if (inspect.ExitCode != 0 || config.GetProperty("NanoCpus").GetInt64() != 4_000_000_000 || config.GetProperty("Memory").GetInt64() != 24L * 1024 * 1024 * 1024 || config.GetProperty("CpusetCpus").GetString() != "4-7")
            {
                throw new InvalidOperationException("Owned database resource budget differs.");
            }

            await WriteAsync(output, "database-budget.json", new
            {
                ContainerId = container.Id,
                CpuSet = "4-7",
                CPUs = 4,
                MemoryGiB = 24
            });
            await using var services = new ServiceCollection().AddDbContext<DimensionsQualificationContext>(options =>
            {
                if (engine == "sqlserver")
                {
                    options.UseSqlServer(connection, sql => sql.MinBatchSize(1));
                }
                else
                {
                    options.UseNpgsql(connection);
                }
            }).BuildServiceProvider();
            var scopes = services.GetRequiredService<IServiceScopeFactory>();
            await using (var scope = services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
                await context.Database.EnsureCreatedAsync();
                await File.WriteAllTextAsync(Path.Combine(output, "schema.sql"), context.Database.GenerateCreateScript());
            }

            if (engine == "sqlserver")
            {
                await ConfigureSqlMaxDopAsync(scopes, 1);
            }

            var fixture = new StorageItemWriteFixture(scopes);
            await fixture.PrepareDatabaseAsync(engine, output);
            var results = new List<object>();
            var previous = 0;
            foreach (var owners in populations)
            {
                await fixture.SetDimensionsAsync(0);
                await fixture.GrowAsync(previous, owners, engine);
                foreach (var dimensions in new[] { 0, 3 })
                {
                    await fixture.SetDimensionsAsync(dimensions);
                    await fixture.VerifyPopulationAsync(owners, dimensions);
                    await using (var statisticsScope = scopes.CreateAsyncScope())
                    {
                        var statisticsContext = statisticsScope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
                        statisticsContext.Database.SetCommandTimeout(600);
                        if (engine == "sqlserver")
                        {
                            await statisticsContext.Database.ExecuteSqlRawAsync("EXEC sys.sp_updatestats");
                        }
                        else
                        {
                            await statisticsContext.Database.ExecuteSqlRawAsync("ANALYZE");
                        }
                    }

                    foreach (var feature in bulkOnly ? new[] { "blob" } : new[] { "blob", "document" })
                    {
                        results.AddRange(await fixture.MeasureAsync(engine, feature, owners, dimensions, smoke, output, bulkOnly));
                        await fixture.VerifyPopulationAsync(owners, dimensions);
                    }
                }

                previous = owners;
            }

            await WriteAsync(output, "results.json", new
            {
                Engine = engine,
                Smoke = smoke,
                OperationChecksPassed = true,
                QualificationComplete = false,
                Results = results
            });
            return 0;
        }
        catch (Exception exception)
        {
            await WriteAsync(output, "failure.json", new
            {
                Type = exception.GetType().Name,
                exception.Message,
                Details = exception.ToString(),
                Stack = exception.StackTrace,
                QualificationComplete = false
            });
            Console.WriteLine($"Item write measurements failed: {exception.Message}");
            return 1;
        }
        finally
        {
            if (container is not null)
            {
                await container.DisposeAsync();
            }
        }
    }
}
