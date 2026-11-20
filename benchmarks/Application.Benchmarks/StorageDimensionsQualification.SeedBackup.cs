// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

public static partial class StorageDimensionsQualification
{
    private static async Task SaveSqlServerSeedAsync(string connection, string containerId, string output, int owners, string feature, string profile)
    {
        // Save only the two run-owned catalogs, before profiling/measurement mutates the seeded fixture.
        var builder = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        await using var database = new SqlConnection(builder.ConnectionString);
        await database.OpenAsync();
        foreach (var name in new[] { "dimensions_qualification", "dimensions_parent_baseline" })
        {
            var remoteFile = $"/var/opt/mssql/data/{name}-seed.bak";
            using var command = database.CreateCommand();
            command.CommandTimeout = 600;
            command.CommandText = $"BACKUP DATABASE [{name}] TO DISK = @file WITH COPY_ONLY, COMPRESSION, INIT;";
            command.Parameters.AddWithValue("@file", remoteFile);
            await command.ExecuteNonQueryAsync();
            var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("cp");
            start.ArgumentList.Add($"{containerId}:{remoteFile}");
            start.ArgumentList.Add(Path.Combine(output, name + "-seed.bak"));
            using var copy = Process.Start(start) ?? throw new InvalidOperationException("Cannot preserve the run-owned SQL fixture.");
            var copied = copy.StandardOutput.ReadToEndAsync();
            var error = copy.StandardError.ReadToEndAsync();
            await copy.WaitForExitAsync();
            await copied;
            if (copy.ExitCode != 0) throw new InvalidOperationException("Cannot preserve the run-owned SQL fixture: " + await error);
            await error;
        }

        await WriteAsync(output, "seed-backup.json", new
        {
            Format = "SQL Server native backup", OwnersPerFeature = owners, Feature = feature, Profile = profile,
            Seed = DimensionQualificationData.Seed, BeforeTimedWorkloads = true,
            SchemaSHA256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(output, "schema.sql")))),
            BaselineSchemaSHA256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(output, "parent-baseline-schema.sql")))),
            Note = "Run-owned fictional fixture only. Not a qualification pass. Any future restoration must verify schema/populations and rerun semantic preflight and warm-up."
        });
        Console.WriteLine("Preserved seeded SQL Server catalogs for further diagnostics.");
    }
}
