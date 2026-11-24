// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Evidence;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.LiteDb;

[IntegrationTest("Infrastructure")]
public class LiteDbFileEventStoreTests
{
    [Fact]
    public async Task Queries_IsolateLocationsAndExactPaths_WithInclusiveUtcRange()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Files;
        var now = host.Clock.GetUtcNow();
        var first = Event("A", "report.csv", now);
        var latest = Event("B", "report.csv", now.AddSeconds(1));
        await sut.StoreEventAsync(first);
        await sut.StoreEventAsync(latest);
        await sut.StoreEventAsync(Event("a", "REPORT.csv", now.AddSeconds(2)));

        (await sut.GetFileEventAsync("report.csv")).Id.ShouldBe(latest.Id);
        (await sut.GetFileEventAsync("A", "report.csv")).Id.ShouldBe(first.Id);
        (await sut.GetFileEventAsync("A", "REPORT.csv")).ShouldBeNull();
        (await sut.GetFileEventsAsync("report.csv", now, now)).Single().Id.ShouldBe(first.Id);
        (await sut.GetFileEventsForLocationAsync("A")).Single().Id.ShouldBe(first.Id);
        (await sut.GetPresentFilesAsync("A")).ShouldBe(["report.csv"]);
        (await sut.GetPresentFilesAsync("a")).ShouldBe(["REPORT.csv"]);
    }

    [Fact]
    public async Task Presence_UsesEventTimeThenArrival_DespiteOutOfOrderDelivery()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Files;
        var now = host.Clock.GetUtcNow();
        await sut.StoreEventAsync(Event("A", "file", now, FileEventType.Deleted));
        await sut.StoreEventAsync(Event("A", "file", now.AddMinutes(-1)));
        (await sut.GetPresentFilesAsync("A")).ShouldBeEmpty();
        var restored = Event("A", "file", now);
        await sut.StoreEventAsync(restored);
        (await sut.GetPresentFilesAsync("A")).ShouldBe(["file"]);
        (await sut.GetFileEventAsync("A", "file")).Id.ShouldBe(restored.Id);
        await sut.StoreEventAsync(Event("A", "file", now, FileEventType.Deleted));
        (await sut.GetPresentFilesAsync("A")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Publication_IsIdempotent_RejectsConflicts_AndDetachesCallerModels()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Files;
        var original = Event("A", "file", host.Clock.GetUtcNow());
        original.Properties["count"] = 3;
        await sut.StoreEventAsync(original);
        await sut.StoreEventAsync(original);
        original.EventType = FileEventType.Deleted;
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.StoreEventAsync(original));
        var stored = (await sut.GetFileEventsForLocationAsync("A")).Single();
        stored.EventType.ShouldBe(FileEventType.Added);
        stored.Properties["count"].ShouldBe(3);
        stored.FilePath = "changed";
        (await sut.GetFileEventAsync("A", "file")).FilePath.ShouldBe("file");
        (await sut.GetPresentFilesAsync("A")).ShouldBe(["file"]);
    }

    [Fact]
    public async Task ProcessingResults_PersistIndependentOutcomes_AndRejectOrphansAndConflicts()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Files;
        var source = Event("A", "file", host.Clock.GetUtcNow());
        await sut.StoreEventAsync(source);
        var result = new FileProcessingResult { FileEventId = source.Id, ProcessorName = "parse", Success = true, ProcessedAt = source.DetectedDate };
        await sut.StoreProcessingResultAsync(result);
        await sut.StoreProcessingResultAsync(result);
        await sut.StoreProcessingResultAsync(new() { FileEventId = source.Id, ProcessorName = "archive", Success = false, Message = "retry", ProcessedAt = source.DetectedDate.AddSeconds(1) });
        result.Success = false;
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.StoreProcessingResultAsync(result));
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.StoreProcessingResultAsync(new() { FileEventId = Guid.NewGuid() }));
        var stored = await sut.GetProcessingResultsAsync(source.Id);
        stored.Select(item => item.ProcessorName).ShouldBe(["parse", "archive"]);
        stored[0].Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Reopen_RetainsEventsOutcomesAndPresence()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-monitor-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "monitor.db");
            var source = Event("A", "file", new EvidenceClock().GetUtcNow());
            await using (var host = new LiteDbEvidenceTestHost(fileEventFile: file))
            {
                await host.Files.StoreEventAsync(source);
                await host.Files.StoreProcessingResultAsync(new() { FileEventId = source.Id, ProcessorName = "parse", Success = true });
            }

            await using (var host = new LiteDbEvidenceTestHost(fileEventFile: file))
            {
                (await host.Files.GetFileEventAsync("A", "file")).Id.ShouldBe(source.Id);
                (await host.Files.GetProcessingResultsAsync(source.Id)).Single().Success.ShouldBeTrue();
                (await host.Files.GetPresentFilesAsync("A")).ShouldBe(["file"]);
            }
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task NativeSelection_FiltersBeforeBound_AndNeverSilentlyTruncates()
    {
        await using var host = new LiteDbEvidenceTestHost(maxRows: 3);
        var sut = host.Files;
        for (var i = 0; i < 6; i++) await sut.StoreEventAsync(Event("A", "file", host.Clock.GetUtcNow().AddSeconds(i)));
        var selected = Event("B", "file", host.Clock.GetUtcNow());
        await sut.StoreEventAsync(selected);
        (await sut.GetFileEventsForLocationAsync("B")).Single().Id.ShouldBe(selected.Id);
        (await sut.GetFileEventAsync("A", "file")).DetectedDate.ShouldBe(host.Clock.GetUtcNow().AddSeconds(5));
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.GetFileEventsForLocationAsync("A"));
    }

    [Fact]
    public async Task CancellationAndInvalidPublication_LeaveNoPresenceOrEvent()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Files;
        var source = Event("A", "file", host.Clock.GetUtcNow());
        await Should.ThrowAsync<OperationCanceledException>(() => sut.StoreEventAsync(source, new CancellationToken(true)));
        source.LocationName = "";
        await Should.ThrowAsync<ArgumentException>(() => sut.StoreEventAsync(source));
        (await sut.GetPresentFilesAsync("A")).ShouldBeEmpty();
        (await sut.GetFileEventAsync("file")).ShouldBeNull();
    }

    private static FileEvent Event(string location, string path, DateTimeOffset at, FileEventType type = FileEventType.Added) =>
        new() { LocationName = location, FilePath = path, DetectedDate = at, EventType = type };
}
