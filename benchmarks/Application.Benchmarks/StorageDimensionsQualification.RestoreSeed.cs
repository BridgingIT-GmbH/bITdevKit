// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Common;
using Application.Storage;

public static partial class StorageDimensionsQualification
{
    private static async Task VerifyRestoredSnapshotsAsync(IServiceScopeFactory scopes, int owners, string feature)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        foreach (var owner in Enumerable.Range(0, Math.Min(80, owners)).Concat(new[] { owners / 2, owners - 1 }).Distinct())
        {
            var expected = DimensionQualificationData.Scalars(owner);
            var keyHash = HashHelper.ComputeSha256(DimensionQualificationData.Key(owner));
            if (feature != "document")
            {
                var containerHash = HashHelper.ComputeSha256(DimensionQualificationData.Scope(owner));
                var json = await context.StorageBlobs.AsNoTracking().Where(row => row.ContainerHash == containerHash && row.NameHash == keyHash)
                    .Select(row => row.DimensionsJson).SingleAsync();
                var parsed = Require(BlobDimensionValues.Deserialize(json));
                if (parsed.Count != expected.Length || expected.Any(entry => !parsed.TryGetValue(entry.Key, out var actual)
                    || actual != Require(BlobDimensionValue.Create(entry.Value))))
                    throw new InvalidDataException("Restored blob snapshot differs from deterministic fixture values.");
            }

            if (feature != "blob")
            {
                var typeHash = HashHelper.ComputeSha256(DimensionQualificationData.Type(owner).Value);
                var partitionHash = HashHelper.ComputeSha256("records");
                var json = await context.StorageDocuments.AsNoTracking().Where(row => row.TypeHash == typeHash && row.PartitionKeyHash == partitionHash && row.RowKeyHash == keyHash)
                    .Select(row => row.DimensionsJson).SingleAsync();
                var parsed = Require(DocumentDimensionValues.Deserialize(json));
                if (parsed.Count != expected.Length || expected.Any(entry => !parsed.TryGetValue(entry.Key, out var actual)
                    || actual != Require(DocumentDimensionValue.Create(entry.Value))))
                    throw new InvalidDataException("Restored document snapshot differs from deterministic fixture values.");
            }
        }

        Console.WriteLine("Restored snapshot kinds/names/values verified across scopes, kinds and dataset boundaries; query membership preflight follows.");
    }

    private static async Task RestoreSqlServerSeedAsync(string connection, string containerId, string source, string output, int owners, string feature, string profile)
    {
        source = Path.GetFullPath(source);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bITdevKit.slnx"))) root = root.Parent;
        var artifacts = root is null ? null : Path.Combine(root.FullName, "artifacts", "storage-dimensions") + Path.DirectorySeparatorChar;
        if (artifacts is null || !source.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Seed input must be a run-owned storage-dimensions artifact directory.");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(source, "seed-backup.json")));
        var seed = manifest.RootElement;
        if (seed.GetProperty("OwnersPerFeature").GetInt32() != owners || seed.GetProperty("Feature").GetString() != feature
            || seed.GetProperty("Profile").GetString() != profile || seed.GetProperty("Seed").GetInt32() != DimensionQualificationData.Seed
            || !seed.GetProperty("BeforeTimedWorkloads").GetBoolean()) throw new ArgumentException("Seed population/profile does not match.");
        foreach (var (file, key) in new[] { ("schema.sql", "SchemaSHA256"), ("parent-baseline-schema.sql", "BaselineSchemaSHA256") })
        {
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(output, file))));
            if (hash != seed.GetProperty(key).GetString()) throw new ArgumentException("Seed schema does not match the current annotated model.");
        }

        // Assembly bytes include the repository's per-build timestamp, so they cannot identify codec compatibility.
        // Logical snapshots are checked against the deterministic fixture after restoration and before any measurements.
        SqlConnection.ClearAllPools();
        var builder = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        await using var database = new SqlConnection(builder.ConnectionString);
        await database.OpenAsync();
        foreach (var name in new[] { "dimensions_qualification", "dimensions_parent_baseline" })
        {
            var localFile = Path.Combine(source, name + "-seed.bak");
            var remoteFile = $"/var/opt/mssql/data/{name}-restore.bak";
            var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("cp"); start.ArgumentList.Add(localFile); start.ArgumentList.Add($"{containerId}:{remoteFile}");
            using var copy = Process.Start(start) ?? throw new InvalidOperationException("Cannot copy owned seed backup.");
            var copied = copy.StandardOutput.ReadToEndAsync(); var error = copy.StandardError.ReadToEndAsync();
            await copy.WaitForExitAsync(); await copied;
            if (copy.ExitCode != 0) throw new InvalidOperationException("Cannot copy seed backup: " + await error);
            await error;
            using var command = database.CreateCommand(); command.CommandTimeout = 600;
            command.CommandText = "RESTORE FILELISTONLY FROM DISK = @file;";
            command.Parameters.AddWithValue("@file", remoteFile);
            var logical = new Dictionary<string, string>();
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) logical.Add(reader["Type"].ToString(), reader["LogicalName"].ToString().Replace("'", "''"));
            if (logical.Count != 2 || !logical.ContainsKey("D") || !logical.ContainsKey("L")) throw new InvalidDataException("Expected one data/log file in owned fixture.");
            // Both names are fixed catalogs created in this invocation's owned container; never target an external database.
            command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{name}] FROM DISK=@file WITH REPLACE, MOVE N'{logical["D"]}' TO N'/var/opt/mssql/data/{name}.mdf', MOVE N'{logical["L"]}' TO N'/var/opt/mssql/data/{name}_log.ldf'; ALTER DATABASE [{name}] SET MULTI_USER;";
            await command.ExecuteNonQueryAsync();
        }

        SqlConnection.ClearAllPools();
        await WriteAsync(output, "restored-seed.json", new { Source = source, OwnersPerFeature = owners, Feature = feature, Profile = profile, SchemaVerified = true, LogicalSnapshotVerificationRequired = true, Note = "Semantic preflight, plans and warm-up still run; restoration is not a qualification pass." });
        Console.WriteLine("Restored owned SQL seed with matching schema and population metadata; checking logical snapshots next.");
    }
}
