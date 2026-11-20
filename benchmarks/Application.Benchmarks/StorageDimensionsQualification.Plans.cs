// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task CapturePlansAsync(IServiceScopeFactory scopes, string engine, string output, IEnumerable<QualificationQuery> queries)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        var directory = Path.Combine(output, "plans");
        Directory.CreateDirectory(directory);
        var index = 0;
        foreach (var query in queries)
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 120;
            command.CommandText = engine switch
            {
                "postgres" => "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Sql,
                "sqlite" => "EXPLAIN QUERY PLAN " + query.Sql,
                _ => "SET STATISTICS XML ON; " + query.Sql + "; SET STATISTICS XML OFF;"
            };
            foreach (var saved in query.Parameters)
            {
                var parameter = saved.Template is ICloneable cloneable ? (DbParameter)cloneable.Clone() : command.CreateParameter();
                parameter.ParameterName = saved.Name;
                if (saved.Template is null) parameter.DbType = saved.Type;
                parameter.Value = saved.Value;
                parameter.Size = saved.Size;
                parameter.Precision = saved.Precision;
                parameter.Scale = saved.Scale;
                command.Parameters.Add(parameter);
            }

            var text = new StringBuilder();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                do
                {
                    while (await reader.ReadAsync())
                    {
                        // PostgreSQL/SQLite EXPLAIN emits only the plan; SQL Server also returns data first.
                        if (engine == "sqlserver" && (reader.FieldCount != 1 || !reader.GetValue(0).ToString().Contains("<ShowPlanXML", StringComparison.Ordinal))) continue;
                        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++) text.Append(reader.GetValue(ordinal)).Append(ordinal == reader.FieldCount - 1 ? '\n' : '\t');
                    }
                } while (await reader.NextResultAsync());
            }

            if (text.Length == 0) throw new InvalidOperationException($"No execution plan was returned for {query.CaseName}.");
            var name = $"{index++:D3}-{query.CaseName?.Replace('/', '-') ?? "query"}.txt";
            await File.WriteAllTextAsync(Path.Combine(directory, name), text.ToString());
            if (engine == "sqlite")
                await WriteAsync(directory, Path.ChangeExtension(name, ".counters.json"), ProfileSqliteQuery((Microsoft.Data.Sqlite.SqliteConnection)context.Database.GetDbConnection(), query));
        }
    }

    private sealed record QualificationParameter(string Name, DbType Type, object Value, int Size, byte Precision, byte Scale, DbParameter Template);

    private sealed record QualificationQuery(string CaseName, string Sql, QualificationParameter[] Parameters);
}
