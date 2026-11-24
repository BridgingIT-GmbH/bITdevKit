// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task<int> ProbeOwnerVisibilityIndexAsync(IServiceScopeFactory scopes, string output, IEnumerable<QualificationQuery> queries)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        // Restore archives may predate the production visibility index. Remove either shape's index
        // before the baseline, so this diagnostic also remains valid against newly created fixtures.
        var productionIndex = context.Model.FindEntityType(typeof(Infrastructure.EntityFramework.StorageDocument)).GetIndexes()
            .SingleOrDefault(index => index.Properties.Select(property => property.Name).SequenceEqual(
                ["TypeHash", "Type", "Id", "ExpiresAtUnixMilliseconds"]));
        await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS [IX___Storage_Documents_TypeHash_Type_Id_ExpiresAtUnixMilliseconds] ON [__Storage_Documents]");
        var selected = queries.Where(query => query.CaseName.EndsWith("/count", StringComparison.Ordinal)
            || (query.CaseName.StartsWith("document/", StringComparison.Ordinal) &&
                (query.CaseName.Contains("/exists/", StringComparison.Ordinal) || query.CaseName.Contains("/non-null/", StringComparison.Ordinal) || query.CaseName.Contains("/null/", StringComparison.Ordinal)))
            || (query.CaseName.StartsWith("blob/", StringComparison.Ordinal) &&
                (query.CaseName.Contains("/exists/", StringComparison.Ordinal) || query.CaseName.Contains("/non-null/", StringComparison.Ordinal)))).ToArray();
        if (selected.Length < 10)
        {
            throw new InvalidOperationException("Owner-index probe requires captured counts and broad blob pages.");
        }

        var expected = new Dictionary<string, string[]>();
        foreach (var query in selected)
        {
            expected[query.CaseName] = await Execute(query);
        }

        var variants = new[]
        {
            (Name: "baseline", Columns: (string)null),
            (Name: "identity-first", Columns: "[TypeHash], [Type], [Id], [ExpiresAtUnixMilliseconds]"),
            (Name: "ordered-type-visibility", Columns: "[TypeHash], [Type], [PartitionKey], [RowKey], [Id], [ExpiresAtUnixMilliseconds]"),
            (Name: "ordered-key-visibility", Columns: "[TypeHash], [PartitionKey], [RowKey], [Type], [Id], [ExpiresAtUnixMilliseconds]")
        };
        var reports = new List<object>();
        await WriteAsync(output, "owner-index-probe-definition.json", new
        {
            DiagnosticOnly = true,
            QualificationComplete = false,
            Rounds = 5,
            SamplesPerQueryPerRound = 5,
            Variants = variants.Select(variant => new { variant.Name, variant.Columns }),
            Cases = selected.Select(query => query.CaseName),
            Note = "Only an owned database changes. Rotating order, warm-up and all result fields are verified. DDL, plans and index size accounting are outside query timings."
        });
        for (var round = 0; round < 5; round++)
        {
            for (var offset = 0; offset < variants.Length; offset++)
            {
                var variant = variants[(round + offset) % variants.Length];
                await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents]");
                await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS [IX___Storage_Documents_TypeHash_PartitionKey_RowKey] ON [__Storage_Documents]");
                if (variant.Name != "ordered-key-visibility")
                {
                    await context.Database.ExecuteSqlRawAsync("CREATE INDEX [IX___Storage_Documents_TypeHash_PartitionKey_RowKey] ON [__Storage_Documents] ([TypeHash], [PartitionKey], [RowKey])");
                }

                var createSql = variant.Name switch
                {
                    "identity-first" => "CREATE INDEX [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents] ([TypeHash], [Type], [Id], [ExpiresAtUnixMilliseconds])",
                    "ordered-type-visibility" => "CREATE INDEX [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents] ([TypeHash], [Type], [PartitionKey], [RowKey], [Id], [ExpiresAtUnixMilliseconds])",
                    "ordered-key-visibility" => "CREATE INDEX [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents] ([TypeHash], [PartitionKey], [RowKey], [Type], [Id], [ExpiresAtUnixMilliseconds])",
                    _ => null
                };
                if (createSql is not null)
                {
                    await context.Database.ExecuteSqlRawAsync(createSql);
                }

                var samples = new List<object>();
                foreach (var query in selected)
                {
                    if (!expected[query.CaseName].SequenceEqual(await Execute(query)))
                    {
                        throw new InvalidOperationException("Owner-index warm-up changed results.");
                    }

                    var times = new List<double>();
                    for (var sample = 0; sample < 5; sample++)
                    {
                        var timer = Stopwatch.StartNew();
                        var actual = await Execute(query);
                        timer.Stop();
                        if (!expected[query.CaseName].SequenceEqual(actual))
                        {
                            throw new InvalidOperationException("Owner-index experiment changed results.");
                        }

                        times.Add(timer.Elapsed.TotalMilliseconds);
                    }

                    samples.Add(new
                    {
                        Case = query.CaseName,
                        Rows = expected[query.CaseName].Length,
                        Milliseconds = times
                    });
                }

                var report = new
                {
                    Round = round + 1,
                    Variant = variant.Name,
                    Columns = variant.Columns,
                    Cases = samples
                };
                reports.Add(report);
                await WriteAsync(output, $"owner-index-{round + 1}-{variant.Name}.json", report);
                if (round == 0)
                {
                    await CapturePlansAsync(scopes, "sqlserver", Path.Combine(output, "owner-index-plans-" + variant.Name), selected);
                    await WriteAsync(output, "owner-index-storage-" + variant.Name + ".json", await CaptureStorageAsync(scopes, "sqlserver"));
                }

                Console.WriteLine($"Owner index probe round {round + 1}: {variant.Name}; all result fields verified.");
            }
        }

        await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS [IX___Storage_Documents_TypeHash_PartitionKey_RowKey] ON [__Storage_Documents]");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX [IX___Storage_Documents_TypeHash_PartitionKey_RowKey] ON [__Storage_Documents] ([TypeHash], [PartitionKey], [RowKey])");
        await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents]");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents] ([TypeHash], [Type], [Id], [ExpiresAtUnixMilliseconds])");
        var booleanReports = new List<object>();
        var booleanVariants = new List<QualificationQuery>();
        foreach (var query in queries.Where(query => query.CaseName.Contains("/boolean/", StringComparison.Ordinal)))
        {
            var sql = Regex.Replace(query.Sql,
                @"CROSS APPLY\s*\(\s*SELECT TOP\(1\) (?<columns>.*?)\s+FROM (?<table>\[__Storage_(?:Blobs|Documents)\]) AS (?<owner>\[[^\]]+\])\s+WHERE (?<where>.*?)\s+ORDER BY (?<order>.*?)\s*\) AS (?<result>\[[^\]]+\])",
                match =>
                {
                    var owner = match.Groups["owner"].Value;
                    var identity = Regex.Match(match.Groups["where"].Value,
                        Regex.Escape(owner) + @"\.\[Id\]\s*=\s*(?<candidate>\[[^\]]+\]\.\[(?:BlobId|DocumentId)\])");
                    if (!identity.Success)
                    {
                        throw new InvalidOperationException("Boolean owner lookup lacks a unique identity predicate.");
                    }

                    var where = Regex.Replace(match.Groups["where"].Value, @"\s+AND\s+" + Regex.Escape(identity.Value), "");
                    if (where == match.Groups["where"].Value)
                    {
                        throw new InvalidOperationException("Cannot isolate the boolean owner lookup.");
                    }

                    var columns = match.Groups["columns"].Value;
                    if (!columns.Contains(owner + ".[Id]", StringComparison.Ordinal))
                    {
                        columns += ", " + owner + ".[Id]";
                    }

                    return "INNER JOIN (SELECT " + columns + " FROM " + match.Groups["table"].Value + " AS " + owner
                        + " WHERE " + where + ") AS " + match.Groups["result"].Value + " ON " + match.Groups["result"].Value + ".[Id] = " + identity.Groups["candidate"].Value;
                }, RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (sql == query.Sql)
            {
                throw new InvalidOperationException("Boolean correlation experiment requires an existing lateral lookup.");
            }

            var variant = query with
            {
                Sql = sql,
                CaseName = query.CaseName + "/owner-join"
            };
            booleanVariants.Add(variant);
            var original = await Execute(query);
            for (var warmup = 0; warmup < 3; warmup++)
            {
                await Execute(query);
                if (!original.SequenceEqual(await Execute(variant)))
                {
                    throw new InvalidOperationException("Boolean join changed ordered results.");
                }
            }

            var before = new List<double>();
            var after = new List<double>();
            for (var sample = 0; sample < 20; sample++)
            {
                foreach (var join in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    var timer = Stopwatch.StartNew();
                    var actual = await Execute(join ? variant : query);
                    timer.Stop();
                    if (!original.SequenceEqual(actual))
                    {
                        throw new InvalidOperationException("Boolean join changed ordered results.");
                    }

                    (join ? after : before).Add(timer.Elapsed.TotalMilliseconds);
                }
            }

            booleanReports.Add(new
            {
                Case = query.CaseName,
                CorrelatedMilliseconds = before,
                OwnerJoinMilliseconds = after,
                OrderedResultsEqual = true
            });
        }

        if (booleanReports.Count == 0)
        {
            throw new InvalidOperationException("Boolean join probe did not receive queries.");
        }

        await CapturePlansAsync(scopes, "sqlserver", Path.Combine(output, "boolean-owner-join-plans"), booleanVariants);
        var membershipReports = new List<object>();
        var membershipVariants = new List<QualificationQuery>();
        foreach (var query in selected.Where(query => !query.CaseName.EndsWith("/count", StringComparison.Ordinal)))
        {
            var sql = Regex.Replace(query.Sql,
                @"FROM\s*\(\s*(?<branches>SELECT .*?)\s*\) AS (?<candidate>\[[^\]]+\])\s+INNER JOIN\s*\(\s*SELECT (?<columns>.*?)\s+FROM (?<table>\[__Storage_(?:Blobs|Documents)\]) AS (?<owner>\[[^\]]+\])\s+WHERE (?<where>.*?)\s*\) AS (?<result>\[[^\]]+\])\s+ON \[[^\]]+\]\.\[(?<identity>BlobId|DocumentId)\] = \[[^\]]+\]\.\[Id\]",
                match =>
                {
                    var branches = Regex.Matches(match.Groups["branches"].Value,
                        @"SELECT \[[^\]]+\]\.\[(?:BlobId|DocumentId)\]\s+FROM (?<table>\[__Storage_(?:Blob|Document)Dimensions\]) AS (?<alias>\[[^\]]+\])\s+WHERE (?<condition>.*?)(?=\s+UNION\s+|$)", RegexOptions.Singleline);
                    if (branches.Count != 4)
                    {
                        throw new InvalidOperationException("Broad owner probe requires four simple positive branches.");
                    }

                    var tables = branches.Select(branch => branch.Groups["table"].Value).Distinct().ToArray();
                    if (tables.Length != 1)
                    {
                        throw new InvalidOperationException("Broad owner probe cannot mix value tables.");
                    }

                    var terms = branches.Select(branch => "(" + branch.Groups["condition"].Value.Replace(branch.Groups["alias"].Value, "[broadValue]", StringComparison.Ordinal) + ")");
                    return "FROM (SELECT " + match.Groups["columns"].Value + " FROM " + match.Groups["table"].Value + " AS " + match.Groups["owner"].Value
                        + " WHERE " + match.Groups["where"].Value + " AND EXISTS (SELECT 1 FROM " + tables[0] + " AS [broadValue] WHERE [broadValue].["
                        + match.Groups["identity"].Value + "] = " + match.Groups["owner"].Value + ".[Id] AND (" + string.Join(" OR ", terms) + "))) AS " + match.Groups["result"].Value;
                }, RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (sql == query.Sql)
            {
                throw new InvalidOperationException("Broad owner probe did not replace the candidate join.");
            }

            var variant = query with
            {
                Sql = sql,
                CaseName = query.CaseName + "/owner-exists"
            };
            membershipVariants.Add(variant);
            var original = await Execute(query);
            for (var warmup = 0; warmup < 3; warmup++)
            {
                await Execute(query);
                if (!original.SequenceEqual(await Execute(variant)))
                {
                    throw new InvalidOperationException("Broad owner probe changed ordered results.");
                }
            }

            var before = new List<double>();
            var after = new List<double>();
            for (var sample = 0; sample < 20; sample++)
            {
                foreach (var exists in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    var timer = Stopwatch.StartNew();
                    var actual = await Execute(exists ? variant : query);
                    timer.Stop();
                    if (!original.SequenceEqual(actual))
                    {
                        throw new InvalidOperationException("Broad owner probe changed ordered results.");
                    }

                    (exists ? after : before).Add(timer.Elapsed.TotalMilliseconds);
                }
            }

            membershipReports.Add(new
            {
                Case = query.CaseName,
                CandidateJoinMilliseconds = before,
                OwnerExistsMilliseconds = after,
                OrderedResultsEqual = true
            });
        }

        await CapturePlansAsync(scopes, "sqlserver", Path.Combine(output, "broad-owner-exists-plans"), membershipVariants);
        await WriteAsync(output, "broad-owner-exists-probe.json", new
        {
            DiagnosticOnly = true,
            Cases = membershipReports
        });
        await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS [IX_Benchmark_OwnerVisibility] ON [__Storage_Documents]");
        if (productionIndex is not null)
        {
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX [IX___Storage_Documents_TypeHash_Type_Id_ExpiresAtUnixMilliseconds] ON [__Storage_Documents] ([TypeHash], [Type], [Id], [ExpiresAtUnixMilliseconds])");
        }

        await WriteAsync(output, "boolean-owner-join-probe.json", new
        {
            DiagnosticOnly = true,
            Cases = booleanReports
        });
        await WriteAsync(output, "owner-index-probe.json", new
        {
            DiagnosticOnly = true,
            QualificationComplete = false,
            OrderedResultsEqual = true,
            Reports = reports
        });
        return 0;

        async Task<string[]> Execute(QualificationQuery query)
        {
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = query.Sql;
            command.CommandTimeout = 120;
            foreach (var saved in query.Parameters)
            {
                var parameter = saved.Template is ICloneable cloneable ? (DbParameter)cloneable.Clone() : command.CreateParameter();
                parameter.ParameterName = saved.Name;
                if (saved.Template is null)
                {
                    parameter.DbType = saved.Type;
                }

                parameter.Value = saved.Value;
                parameter.Size = saved.Size;
                parameter.Precision = saved.Precision;
                parameter.Scale = saved.Scale;
                command.Parameters.Add(parameter);
            }

            var rows = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(string.Join("\0", Enumerable.Range(0, reader.FieldCount)
                .Select(index => reader.GetValue(index) is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture))));
            }

            return rows.ToArray();
        }
    }
}