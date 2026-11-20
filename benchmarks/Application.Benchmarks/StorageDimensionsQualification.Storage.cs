// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task<object> CaptureCurrentStorageAsync(IServiceScopeFactory scopes, string engine, string output)
    {
        try { return await CaptureStorageAsync(scopes, engine); }
        catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("dbstat", StringComparison.Ordinal))
        {
            return await CaptureSqliteFootprintAsync(Path.Combine(output, "owned-fixture.db"));
        }
    }

    private static async Task RefreshStatisticsAsync(IServiceScopeFactory scopes, string engine)
    {
        await using var scope = scopes.CreateAsyncScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>(),
            scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>()
        ];
        foreach (var context in contexts)
        {
            context.Database.SetCommandTimeout(600);
            await context.Database.ExecuteSqlRawAsync(engine == "sqlserver" ? "EXEC sys.sp_updatestats" : "ANALYZE");
        }
    }

    private static async Task<JsonElement> CaptureSqliteFootprintAsync(string database)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bITdevKit.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Cannot locate the footprint inspector.");
        var modules = Path.Combine(root.FullName, "artifacts", "storage-dimensions", "sqlite-tools");
        if (!Directory.Exists(modules)) throw new InvalidOperationException("Install the isolated APSW footprint inspector as documented in STORAGE-DIMENSIONS.md.");
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-B");
        start.ArgumentList.Add(Path.Combine(root.FullName, "benchmarks", "Application.Benchmarks", "StorageDimensionsSqliteFootprint.py"));
        start.ArgumentList.Add(modules);
        start.ArgumentList.Add(database);
        start.Environment["PYTHONUTF8"] = "1";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the read-only footprint inspector.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("Read-only footprint inspection failed: " + error);
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }
}
