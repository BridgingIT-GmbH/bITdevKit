// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

public static partial class StorageDimensionsQualification
{
    private static async Task SavePostgresSeedAsync(string connection, string containerId, string output, int owners, string feature, string profile)
    {
        var username = new NpgsqlConnectionStringBuilder(connection).Username;
        var digests = new Dictionary<string, string>();
        foreach (var name in new[] { "dimensions_qualification", "dimensions_parent_baseline" })
        {
            var filename = name + "-seed.dump";
            await RunOwnedDockerAsync("exec", containerId, "pg_dump", "-U", username, "-Fc", "-f", "/tmp/" + filename, name);
            var local = Path.Combine(output, filename);
            await RunOwnedDockerAsync("cp", containerId + ":/tmp/" + filename, local);
            await using var archive = File.OpenRead(local);
            digests.Add(filename, Convert.ToHexString(await SHA256.HashDataAsync(archive)));
        }

        await WriteAsync(output, "seed-backup.json", new
        {
            Format = "PostgreSQL custom archive", OwnersPerFeature = owners, Feature = feature, Profile = profile,
            Seed = DimensionQualificationData.Seed, BeforeTimedWorkloads = true, ArchiveSHA256 = digests,
            SchemaSHA256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(output, "schema.sql")))),
            BaselineSchemaSHA256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(output, "parent-baseline-schema.sql")))),
            Note = "Run-owned fictional fixture only. Restoration must verify schemas, snapshots and query membership; no performance result is inherited."
        });
        Console.WriteLine("Preserved seeded PostgreSQL catalogs for further diagnostics.");
    }

    private static async Task RestorePostgresSeedAsync(string connection, string containerId, string source, string output, int owners, string feature, string profile)
    {
        source = Path.GetFullPath(source);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bITdevKit.slnx"))) root = root.Parent;
        var artifacts = root is null ? null : Path.Combine(root.FullName, "artifacts", "storage-dimensions") + Path.DirectorySeparatorChar;
        if (artifacts is null || !source.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Seed input must be a run-owned storage-dimensions artifact directory.");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(source, "seed-backup.json")));
        var seed = manifest.RootElement;
        if (seed.GetProperty("Format").GetString() != "PostgreSQL custom archive"
            || seed.GetProperty("OwnersPerFeature").GetInt32() != owners || seed.GetProperty("Feature").GetString() != feature
            || seed.GetProperty("Profile").GetString() != profile || seed.GetProperty("Seed").GetInt32() != DimensionQualificationData.Seed
            || !seed.GetProperty("BeforeTimedWorkloads").GetBoolean()) throw new ArgumentException("Seed format/population/profile does not match.");
        foreach (var (file, key) in new[] { ("schema.sql", "SchemaSHA256"), ("parent-baseline-schema.sql", "BaselineSchemaSHA256") })
            if (Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(output, file)))) != seed.GetProperty(key).GetString())
                throw new ArgumentException("Seed schema does not match the current annotated model.");

        NpgsqlConnection.ClearAllPools();
        var username = new NpgsqlConnectionStringBuilder(connection).Username;
        foreach (var name in new[] { "dimensions_qualification", "dimensions_parent_baseline" })
        {
            var filename = name + "-seed.dump";
            var local = Path.Combine(source, filename);
            await using (var archive = File.OpenRead(local))
                if (Convert.ToHexString(await SHA256.HashDataAsync(archive)) != seed.GetProperty("ArchiveSHA256").GetProperty(filename).GetString())
                    throw new InvalidDataException("Owned seed archive digest does not match.");
            await RunOwnedDockerAsync("cp", local, containerId + ":/tmp/" + filename);
            // Fixed catalogs in this invocation's owned container; no external connection is accepted.
            await RunOwnedDockerAsync("exec", containerId, "pg_restore", "-U", username, "--exit-on-error", "--clean", "--if-exists", "--no-owner", "--no-acl", "-d", name, "/tmp/" + filename);
        }

        NpgsqlConnection.ClearAllPools();
        await WriteAsync(output, "restored-seed.json", new { Source = source, OwnersPerFeature = owners, Feature = feature, Profile = profile, SchemaVerified = true, ArchiveDigestsVerified = true, LogicalSnapshotVerificationRequired = true });
        Console.WriteLine("Restored owned PostgreSQL seed; checking logical snapshots and query membership next.");
    }

    private static async Task RunOwnedDockerAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start owned database archive command.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        if (process.ExitCode != 0) throw new InvalidOperationException("Owned database archive command failed: " + await error);
        await error;
    }
}
