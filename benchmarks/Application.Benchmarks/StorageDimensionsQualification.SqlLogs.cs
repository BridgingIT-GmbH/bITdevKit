// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task ProvisionSqlLogsAsync(IServiceScopeFactory scopes, string output)
    {
        const int MinimumLogSizeMiB = 8192;
        var logs = new List<object>();
        foreach (var parentOnly in new[] { true, false })
        {
            await using var scope = scopes.CreateAsyncScope();
            DbContext context = parentOnly ? scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>() : scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
            await context.Database.OpenConnectionAsync();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 600;
            command.CommandText = "SELECT name,CONVERT(bigint,size)*8192 FROM sys.database_files WHERE type=1";
            var files = new List<(string Name, long Bytes)>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) files.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            foreach (var file in files)
            {
                if (file.Bytes < MinimumLogSizeMiB * 1024L * 1024)
                {
                    command.CommandText = "DECLARE @sql nvarchar(max)=N'ALTER DATABASE '+QUOTENAME(DB_NAME())+N' MODIFY FILE (NAME='+QUOTENAME(@file,N'''' )+N', SIZE=8192MB, FILEGROWTH=512MB)'; EXEC sys.sp_executesql @sql;";
                    var name = command.CreateParameter(); name.ParameterName = "@file"; name.Value = file.Name; command.Parameters.Add(name);
                    await command.ExecuteNonQueryAsync();
                    command.Parameters.Clear();
                }

                command.CommandText = "SELECT CONVERT(bigint,size)*8192 FROM sys.database_files WHERE type=1 AND name=@file";
                var verify = command.CreateParameter(); verify.ParameterName = "@file"; verify.Value = file.Name; command.Parameters.Add(verify);
                var actual = Convert.ToInt64(await command.ExecuteScalarAsync());
                command.Parameters.Clear();
                if (actual < MinimumLogSizeMiB * 1024L * 1024) throw new InvalidOperationException("The owned benchmark log was not provisioned before measurement.");
                logs.Add(new { ParentOnly = parentOnly, Database = context.Database.GetDbConnection().Database, File = file.Name, BeforeBytes = file.Bytes, ProvisionedBytes = actual });
            }
        }

        await WriteAsync(output, "sql-log-provisioning.json", new
        {
            MinimumLogSizeMiB = MinimumLogSizeMiB, Logs = logs,
            Note = "Both models receive the same log-capacity floor before any measured interval. Existing larger files are retained. Allocation occurs outside measurements; commit durability, recovery mode and acceptance gates are unchanged."
        });
        Console.WriteLine("Owned SQL transaction logs provisioned before measurement for both models.");
    }
}
