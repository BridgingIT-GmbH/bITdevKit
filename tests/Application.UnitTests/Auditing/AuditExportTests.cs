// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public sealed class AuditExportTests
{
    [Fact]
    public async Task Ndjson_UsesExactRecordFramesForDigestAndOmitsSourceEnvelope()
    {
        await using var host = Host();
        await Capture(host);
        await using var scope = host.CreateAsyncScope();
        var session = (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value;
        using var output = new MemoryStream();
        var confirmed = await session.WriteAsync(output);
        confirmed.IsSuccess.ShouldBeTrue();
        confirmed.Value.OutputConfirmed.ShouldBeTrue();
        confirmed.Value.ClientConfirmed.ShouldBeFalse();
        confirmed.Value.SourceRecordCount.ShouldBe(1);
        confirmed.Value.ResponseByteCount.ShouldBe(output.Length);
        var frames = Frames(output.ToArray());
        frames.Count.ShouldBe(3);
        using var metadata = JsonDocument.Parse(frames[0]);
        metadata.RootElement.GetProperty("kind").GetString().ShouldBe("metadata");
        using var data = JsonDocument.Parse(frames[1]);
        data.RootElement.GetProperty("kind").GetString().ShouldBe("record");
        data.RootElement.GetProperty("record").GetProperty("details").GetProperty("record").TryGetProperty("integrityProtection", out _).ShouldBeFalse();
        var digest = Convert.ToHexStringLower(SHA256.HashData(frames[1]));
        confirmed.Value.Sha256.ShouldBe(digest);
        confirmed.Value.DataByteCount.ShouldBe(frames[1].Length);
        using var terminal = JsonDocument.Parse(frames[2]);
        terminal.RootElement.GetProperty("completion").GetProperty("sha256").GetString().ShouldBe(digest);
        terminal.RootElement.GetProperty("completion").GetProperty("outputConfirmed").GetBoolean().ShouldBeFalse();
        terminal.RootElement.GetProperty("completion").TryGetProperty("progress", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RecordLimit_ResumesSameAbsoluteWindowAndCheckpointIncludesLatePersistedRecords()
    {
        await using var host = Host(options => options with { RecordLimit = 1 });
        await Capture(host); await Capture(host);
        await using var scope = host.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
        using var firstOutput = new MemoryStream();
        var first = await (await exporter.OpenAsync()).Value.WriteAsync(firstOutput);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Status.ShouldBe("Partial");
        first.Value.ReasonCode.ShouldBe("RecordLimit");
        first.Value.Progress.IsCheckpoint.ShouldBeFalse();
        using var secondOutput = new MemoryStream();
        var second = await (await exporter.OpenAsync(new() { Resume = first.Value.Progress })).Value.WriteAsync(secondOutput);
        second.IsSuccess.ShouldBeTrue();
        second.Value.Status.ShouldBe("Completed");
        second.Value.Metadata.AsOfUtc.ShouldBe(first.Value.Metadata.AsOfUtc);
        second.Value.Progress.IsCheckpoint.ShouldBeTrue();
        var storage = host.GetRequiredService<IAuditingStorageProvider>();
        var late = AuditRecordCodec.Freeze(new AuditRecord { AuditId = Guid.NewGuid(), Key = "customers.late", DisplayName = "Late", StartedUtc = first.Value.Metadata.AsOfUtc.AddHours(-1), CompletedUtc = first.Value.Metadata.AsOfUtc.AddMinutes(-30), Outcome = AuditOutcome.Succeeded }).Value;
        (await storage.AppendAsync(late)).IsSuccess.ShouldBeTrue();
        using var thirdOutput = new MemoryStream();
        var third = await (await exporter.OpenAsync(new() { Resume = second.Value.Progress })).Value.WriteAsync(thirdOutput);
        third.IsSuccess.ShouldBeTrue();
        third.Value.SourceRecordCount.ShouldBe(1);
        third.Value.LastPosition.Value.ShouldBeGreaterThan(second.Value.LastPosition.Value);
        third.Value.Metadata.AsOfUtc.ShouldBe(first.Value.Metadata.AsOfUtc);
    }

    [Fact]
    public async Task ByteLimit_LeavesNextWholeRecordUnconsumedAndRejectsOversizedEmptyChunk()
    {
        await using var measure = Host();
        await Capture(measure);
        await using var measureScope = measure.CreateAsyncScope();
        using var measured = new MemoryStream();
        (await (await measureScope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value.WriteAsync(measured)).IsSuccess.ShouldBeTrue();
        var length = Frames(measured.ToArray())[1].Length;
        await using var host = Host(options => options with { MetadataByteReserve = 4096, CompletionByteReserve = 4096, ResponseByteLimit = 8192 + length + 128 });
        await Capture(host); await Capture(host);
        await using var scope = host.CreateAsyncScope();
        using var output = new MemoryStream();
        var partial = await (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value.WriteAsync(output);
        partial.IsSuccess.ShouldBeTrue();
        partial.Value.Status.ShouldBe("Partial");
        partial.Value.SourceRecordCount.ShouldBe(1);
        partial.Value.LastPosition.ShouldBe(1);
        output.Length.ShouldBeLessThanOrEqualTo(8192 + length + 128);
        await using var tooSmall = Host(options => options with { MetadataByteReserve = 4096, CompletionByteReserve = 4096, ResponseByteLimit = 8193 });
        await Capture(tooSmall);
        await using var smallScope = tooSmall.CreateAsyncScope();
        using var smallOutput = new MemoryStream();
        var failed = await (await smallScope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value.WriteAsync(smallOutput);
        failed.IsFailure.ShouldBeTrue();
        var disposition = failed.Errors.OfType<AuditExportFailureError>().Single().Completion;
        disposition.ReasonCode.ShouldBe("AuditExportRecordTooLarge");
        disposition.Progress.ShouldBeNull();
    }

    [Fact]
    public async Task Csv_PreservesJsonCellsAndNeutralizesFormulaPrefixesWithCrlfFraming()
    {
        await using var host = Host();
        await Capture(host, " \t=SUM(1,1)");
        await using var scope = host.CreateAsyncScope();
        using var output = new MemoryStream();
        var completion = await (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync(new() { Format = "csv" })).Value.WriteAsync(output);
        completion.IsSuccess.ShouldBeTrue();
        var text = Encoding.UTF8.GetString(output.ToArray());
        text.ShouldStartWith(string.Join(',', CsvAuditExportFormatter.Columns) + "\r\n");
        text.ShouldEndWith("\r\n");
        var rows = ParseCsv(text);
        rows.Count.ShouldBe(2);
        rows[1].Count.ShouldBe(17);
        rows[1][9].ShouldBe("' \t=SUM(1,1)");
        using var details = JsonDocument.Parse(rows[1][16]);
        details.RootElement.GetProperty("note").GetProperty("data").GetString().ShouldBe("first\r\n\"second\"");
        completion.Value.Transformations["FormulaNeutralized"].ShouldBe(1);
        completion.Value.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(output.ToArray())));
        completion.Value.DataByteCount.ShouldBe(output.Length);
    }

    [Fact]
    public async Task ExactAuditId_OmitsDefaultRangeButBroaderOrCannotBypassIt()
    {
        await using var host = Host();
        var old = AuditRecordCodec.Freeze(new AuditRecord { AuditId = Guid.NewGuid(), Key = "customers.old", DisplayName = "Old", StartedUtc = DateTimeOffset.UtcNow.AddYears(-1), CompletedUtc = DateTimeOffset.UtcNow.AddYears(-1).AddMinutes(1), Outcome = AuditOutcome.Succeeded }).Value;
        (await host.GetRequiredService<IAuditingStorageProvider>().AppendAsync(old)).IsSuccess.ShouldBeTrue();
        await using var scope = host.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
        using var exactOutput = new MemoryStream();
        var exact = await (await exporter.OpenAsync(new() { Filter = new() { Filters = [new("AuditId", FilterOperator.Equal, old.AuditId)] } })).Value.WriteAsync(exactOutput);
        exact.IsSuccess.ShouldBeTrue(); exact.Value.SourceRecordCount.ShouldBe(1);
        var broad = new FilterModel { Filters = [new("AuditId", FilterOperator.Equal, old.AuditId) { Logic = FilterLogicOperator.Or }, new("Key", FilterOperator.Equal, "customers.old")] };
        using var broadOutput = new MemoryStream();
        var guarded = await (await exporter.OpenAsync(new() { Filter = broad })).Value.WriteAsync(broadOutput);
        guarded.IsSuccess.ShouldBeTrue(); guarded.Value.SourceRecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task Admission_IsBoundedAndDisposeReleasesUnusedAttempts()
    {
        await using var host = Host();
        await using var scope = host.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
        var sessions = new List<AuditExportSession>();
        try
        {
            for (var index = 0; index < 4; index++) sessions.Add((await exporter.OpenAsync()).Value);
            (await exporter.OpenAsync()).IsFailure.ShouldBeTrue();
            await sessions[0].DisposeAsync();
            var available = await exporter.OpenAsync();
            available.IsSuccess.ShouldBeTrue();
            await available.Value.DisposeAsync();
        }
        finally { foreach (var session in sessions) await session.DisposeAsync(); }
    }

    [Fact]
    public async Task FailedFinalFlush_LeavesNoConfirmedProgress()
    {
        await using var host = Host();
        await Capture(host);
        await using var scope = host.CreateAsyncScope();
        var session = (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync()).Value;
        using var output = new FinalFlushFailure();
        var result = await session.WriteAsync(output);
        result.IsFailure.ShouldBeTrue();
        var failed = result.Errors.OfType<AuditExportFailureError>().Single().Completion;
        failed.OutputConfirmed.ShouldBeFalse();
        failed.Progress.ShouldBeNull();
    }

    [Fact]
    public async Task Csv_LargeActionArray_PreservesCompleteJson()
    {
        var actions = Enumerable.Range(0, 80).Select(index => new AuditAction
        {
            Id = Guid.NewGuid(), Sequence = index, Key = "customers.save", DisplayName = new string('x', 500)
        }).ToImmutableArray();
        var source = new AuditExportRecord { SourceStoreId = Guid.NewGuid(), Details = new() { Record = new() { AuditId = Guid.NewGuid(), Key = "customers.import", Actions = actions } } };

        using var frame = await new CsvAuditExportFormatter().RecordAsync(source, new() { GraphByteLimit = 8_388_608 });

        var cells = ParseCsv(Encoding.UTF8.GetString(frame.Bytes.Span)).ShouldHaveSingleItem();
        using var json = JsonDocument.Parse(cells[14]);
        json.RootElement.GetArrayLength().ShouldBe(80);
        json.RootElement[79].GetProperty("displayName").GetString().ShouldBe(new string('x', 500));
    }

    [Fact]
    public async Task Csv_ChargesActualEscapedBytesAtExactBoundary()
    {
        var formatter = new CsvAuditExportFormatter();
        var source = new AuditExportRecord { SourceStoreId = Guid.NewGuid(), Details = new() { Record = new() { AuditId = Guid.NewGuid(), Key = "x", DisplayName = "x", StartedUtc = DateTimeOffset.UtcNow, CompletedUtc = DateTimeOffset.UtcNow } } };
        using var row = await formatter.RecordAsync(source, new() { GraphByteLimit = 512 });
        row.Bytes.Length.ShouldBeLessThan(512);
        using var exact = await formatter.RecordAsync(source, new() { GraphByteLimit = row.Bytes.Length });
        exact.Bytes.ToArray().ShouldBe(row.Bytes.ToArray());
        await Should.ThrowAsync<AuditExportException>(() => formatter.RecordAsync(source, new() { GraphByteLimit = row.Bytes.Length - 1 }));
    }

    [Fact]
    public async Task DisposedSession_CannotStartConfirmationAndCancellationRetainsCapacityUntilDependencyExits()
    {
        await using var host = Host(options => options with { ActiveExportLimit = 1, MaxDuration = TimeSpan.FromMilliseconds(200), ProviderReadTimeout = TimeSpan.FromMilliseconds(100) });
        await Capture(host);
        await using var scope = host.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
        var session = (await exporter.OpenAsync()).Value;
        await session.DisposeAsync();
        var invalid = await session.ConfirmAsync(new(NdjsonStreamStatus.Succeeded, true, true, 0, 0, 0, 0, false));
        invalid.IsFailure.ShouldBeTrue();
        var next = (await exporter.OpenAsync()).Value;
        using var destination = new GatedDestination();
        var writing = next.WriteAsync(destination);
        await destination.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var ended = await writing.WaitAsync(TimeSpan.FromSeconds(2));
        ended.IsFailure.ShouldBeTrue();
        (await exporter.OpenAsync()).IsFailure.ShouldBeTrue();
        destination.Release.TrySetResult();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            var available = await exporter.OpenAsync();
            if (available.IsSuccess) { await available.Value.DisposeAsync(); break; }

            await Task.Delay(10, deadline.Token);
        }
    }

    [Fact]
    public async Task RemovalAfterDataBeforeConfirmation_InvalidatesAlreadyEmittedEvidence()
    {
        await using var host = Host(); await Capture(host);
        await using var scope = host.CreateAsyncScope();
        var session = (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync(new() { Filter = new() { Filters = [new("StorageState", FilterOperator.IsNotNull, null)] } })).Value;
        long count = 0, bytes = 0;
        await foreach (var frame in session.ReadFramesAsync()) { using (frame) { count++; bytes += frame.Bytes.Length; } }

        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        (await provider.ArchiveBeforeAsync(DateTimeOffset.UtcNow.AddMinutes(1))).IsSuccess.ShouldBeTrue();
        (await provider.RemoveArchivedBeforeAsync(DateTimeOffset.UtcNow.AddMinutes(1))).IsSuccess.ShouldBeTrue();
        var result = await session.ConfirmAsync(new(NdjsonStreamStatus.Succeeded, true, true, count, bytes, count, bytes, false));
        result.IsFailure.ShouldBeTrue();
        await session.DisposeAsync();
    }

    public sealed class GatedDestination : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.Entered.TrySetResult(); await this.Release.Task;
            await base.WriteAsync(buffer, cancellationToken);
        }
    }

    [Fact]
    public async Task UncooperativeWindowOpen_IsBoundedAndRetainsItsAdmissionUntilActuallyFinished()
    {
        var provider = Substitute.For<IAuditingStorageProvider>();
        provider.Capabilities.Returns(new AuditProviderCapabilities { SupportsResumableExtraction = true });
        var pending = new TaskCompletionSource<Result<AuditExtractionCursor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.OpenExtractionAsync(Arg.Any<AuditExtractionPlan>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var services = new ServiceCollection(); services.AddLogging();
        services.AddAuditing().WithExports(options => options with { ActiveExportLimit = 1, ProviderReadTimeout = TimeSpan.FromMilliseconds(50), MaxDuration = TimeSpan.FromMilliseconds(100) });
        services.AddSingleton(provider);
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var exports = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
        (await exports.OpenAsync().WaitAsync(TimeSpan.FromSeconds(2))).IsFailure.ShouldBeTrue();
        var busy = await exports.OpenAsync(); busy.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditExportBusy");
        pending.TrySetResult(Result<AuditExtractionCursor>.Failure(new AuditError("AuditReadUnavailable")));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (true)
        {
            using var admission = host.GetRequiredService<AuditExportLimiter>().TryAcquire();
            if (admission is not null) break;
            await Task.Delay(10, timeout.Token);
        }
    }

    private static ServiceProvider Host(Func<AuditExportOptions, AuditExportOptions> configure = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        var builder = services.AddAuditing();
        if (configure is not null) builder.WithExports(configure);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task Capture(IServiceProvider host, string displayName = "Rename customer")
    {
        await using var scope = host.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAuditContext>().RunAsync(builder => builder.Key("customers.rename").DisplayName(displayName), (capture, _) =>
        {
            capture.SetDetail("note", AuditValue.From("first\r\n\"second\""));
            return Task.FromResult(1);
        });
    }

    private static List<byte[]> Frames(byte[] bytes)
    {
        var frames = new List<byte[]>(); var start = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'\n') continue;
            frames.Add(bytes[start..(index + 1)]); start = index + 1;
        }

        start.ShouldBe(bytes.Length); return frames;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var value = new StringBuilder(); var quote = false;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '"') { if (quote && index + 1 < text.Length && text[index + 1] == '"') { value.Append('"'); index++; } else quote = !quote; }
            else if (!quote && current == ',') { row.Add(value.ToString()); value.Clear(); }
            else if (!quote && current == '\r' && index + 1 < text.Length && text[index + 1] == '\n') { row.Add(value.ToString()); value.Clear(); rows.Add(row); row = []; index++; }
            else value.Append(current);
        }

        quote.ShouldBeFalse(); return rows;
    }

    public sealed class FinalFlushFailure : MemoryStream
    {
        private int flushes;
        public override Task FlushAsync(CancellationToken cancellationToken) => ++this.flushes == 3 ? Task.FromException(new IOException("private-flush-error")) : Task.CompletedTask;
    }
}
