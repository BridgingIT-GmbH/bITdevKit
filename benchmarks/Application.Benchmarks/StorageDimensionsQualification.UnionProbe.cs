// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task ProbeUnionAllAsync(IServiceScopeFactory scopes, string output, IEnumerable<QualificationQuery> queries)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        var selected = queries.Where(query => query.CaseName.StartsWith("blob/", StringComparison.Ordinal)
            && (query.CaseName.Contains("integer-range", StringComparison.Ordinal) || query.CaseName.Contains("datetime-range", StringComparison.Ordinal))).ToArray();
        if (selected.Length != 4) throw new InvalidOperationException("Expected the four first/late range experiments.");
        var reports = new List<object>(); var variants = new List<QualificationQuery>();
        foreach (var query in selected)
        {
            var sql = Regex.Replace(query.Sql, @"\bUNION\b(?!\s+ALL\b)", "UNION ALL", RegexOptions.CultureInvariant);
            var existingUnion = sql != query.Sql;
            var variant = query with { Sql = sql, CaseName = query.CaseName + (existingUnion ? "/union-all" : "/combined-row") }; variants.Add(variant);
            var expected = await Execute(query);
            for (var warm = 0; warm < 3; warm++)
            {
                await Execute(query);
                var actual = await Execute(variant);
                if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Union experiment changed ordered result keys.");
            }

            var before = new List<double>(); var after = new List<double>();
            for (var iteration = 0; iteration < 20; iteration++)
            {
                foreach (var all in iteration % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    var timer = Stopwatch.StartNew(); var actual = await Execute(all ? variant : query); timer.Stop();
                    if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Union experiment changed ordered result keys.");
                    (all ? after : before).Add(timer.Elapsed.TotalMilliseconds);
                }
            }

            reports.Add(new { Case = query.CaseName, ExistingCandidateUnion = existingUnion, Rows = expected.Count, OriginalMilliseconds = before, UnionAllMilliseconds = after, OrderedKeysEqual = true });
        }

        await CapturePlansAsync(scopes, "sqlserver", Path.Combine(output, "union-all-probe"), variants);
        await WriteAsync(output, "union-all-probe.json", new { DiagnosticOnly = true, QualificationComplete = false, Cases = reports, Note = "Read-only SQL experiment, not mixed-load acceptance. Without an existing UNION, both timing lists sample the same current combined-row query." });
        Console.WriteLine("Union experiment complete; ordered keys verified for all alternating samples.");

        async Task<List<string>> Execute(QualificationQuery query)
        {
            using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = query.Sql; command.CommandTimeout = 120;
            foreach (var saved in query.Parameters)
            {
                var parameter = saved.Template is ICloneable cloneable ? (DbParameter)cloneable.Clone() : command.CreateParameter();
                parameter.ParameterName = saved.Name; if (saved.Template is null) parameter.DbType = saved.Type;
                parameter.Value = saved.Value; parameter.Size = saved.Size; parameter.Precision = saved.Precision; parameter.Scale = saved.Scale; command.Parameters.Add(parameter);
            }

            var keys = new List<string>(); await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) keys.Add(reader.GetString(0) + "\0" + reader.GetString(1));
            return keys;
        }
    }
}
