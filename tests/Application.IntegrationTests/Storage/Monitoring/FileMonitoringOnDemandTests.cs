// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using static BridgingIT.DevKit.Application.IntegrationTests.Storage.FileMonitoringConfigurationTests;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class FileMonitoringOnDemandTests(ITestOutputHelper output)
{
    [Fact]
    public async Task FileMonitoringService_OnDemand_DetectsFileChanges()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.UseProcessor<FileLoggerProcessor>();
                    //options.UseProcessor<FileMoverProcessor>(config =>
                    //    config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(tempFolder, "MovedDocs")));
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var handlers = provider.GetServices<ILocationHandler>();
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content");
        await Task.Delay(500); // Allow processing

        // Act
        //await sut.StartAsync(CancellationToken.None);
        var scanContext = await sut.ScanLocationAsync("Docs", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert
        scanContext.Events.ShouldHaveSingleItem();
        scanContext.Events[0].EventType.ShouldBe(FileEventType.Added);
        scanContext.Events[0].FilePath.ShouldBe("test.txt");
        //var movedExists = File.Exists(Path.Combine(tempFolder, "MovedDocs", "test.txt"));
        //movedExists.ShouldBeTrue();

        // Cleanup(optional)
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_CallsProcessorForEachEvent()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.UseProcessor<TestProcessor>(); // Custom processor
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Initial state: Create multiple files
        var files = new[]
        {
            Path.Combine(tempFolder, "file1.txt"),
            Path.Combine(tempFolder, "file2.txt"),
            Path.Combine(tempFolder, "file3.txt")
        };
        File.WriteAllText(files[0], "Content 1"); // New file
        File.WriteAllText(files[1], "Content 2"); // New file
        File.WriteAllText(files[2], "Content 3"); // New file

        // Act: Start and scan to trigger processor
        //await sut.StartAsync(CancellationToken.None);
        var scanContext = await sut.ScanLocationAsync("Docs", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Retrieve the TestProcessor instance from the handler
        var handler = provider.GetServices<ILocationHandler>().First(h => h.Options.LocationName == "Docs");
        var processor = handler.GetProcessors()
            .OfType<TestProcessor>()
            .First(); // Assumes TestProcessor is unique; adjust if multiple processors exist

        // Assert
        scanContext.Events.Count.ShouldBe(3); // 3 Added events
        processor.InvocationCount.ShouldBe(3); // Processor called for each event
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(3); // 3 events stored
        allStoredEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        // Cleanup (optional)
         await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_HandlesMultipleFilesWithDifferentEventTypes()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Initial state: Create some files
        var file1 = Path.Combine(tempFolder, "file1.txt");
        var file2 = Path.Combine(tempFolder, "file2.txt");
        var file3 = Path.Combine(tempFolder, "file3.txt");
        File.WriteAllText(file1, "Content 1"); // Will remain unchanged
        File.WriteAllText(file2, "Content 2"); // Will be modified
        File.WriteAllText(file3, "Content 3"); // Will be deleted

        // First scan to establish baseline
        //await sut.StartAsync(CancellationToken.None);
        await sut.ScanLocationAsync("Docs", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Modify file system state
        var file4 = Path.Combine(tempFolder, "file4.txt");
        File.WriteAllText(file2, "Updated Content 2"); // Modify file2
        File.Delete(file3); // Delete file3
        File.WriteAllText(file4, "Content 4"); // Add file4

        // Act: Second scan to detect changes
        var scanContext = await sut.ScanLocationAsync("Docs", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(6); // 3 initial Added + 1 Changed + 1 Deleted + 1 Added

        // File 1: Unchanged, only initial Added
        var file1Events = await store.GetFileEventsAsync("file1.txt");
        file1Events.Count().ShouldBe(1);
        file1Events.First().EventType.ShouldBe(FileEventType.Added);

        // File 2: Initial Added + Changed
        var file2Events = await store.GetFileEventsAsync("file2.txt");
        file2Events.Count().ShouldBe(2);
        var file2Ordered = file2Events.OrderBy(e => e.DetectedDate).ToList();
        file2Ordered[0].EventType.ShouldBe(FileEventType.Added);
        file2Ordered[1].EventType.ShouldBe(FileEventType.Changed);

        // File 3: Initial Added + Deleted
        var file3Events = await store.GetFileEventsAsync("file3.txt");
        file3Events.Count().ShouldBe(2);
        var file3Ordered = file3Events.OrderBy(e => e.DetectedDate).ToList();
        file3Ordered[0].EventType.ShouldBe(FileEventType.Added);
        file3Ordered[1].EventType.ShouldBe(FileEventType.Deleted);

        // File 4: New Added
        var file4Events = await store.GetFileEventsAsync("file4.txt");
        file4Events.Count().ShouldBe(1);
        file4Events.First().EventType.ShouldBe(FileEventType.Added);

        // Verify scan context
        scanContext.Events.Count.ShouldBe(3); // Changed (file2), Deleted (file3), Added (file4)
        scanContext.Events.ShouldContain(e => e.FilePath == "file2.txt" && e.EventType == FileEventType.Changed);
        scanContext.Events.ShouldContain(e => e.FilePath == "file3.txt" && e.EventType == FileEventType.Deleted);
        scanContext.Events.ShouldContain(e => e.FilePath == "file4.txt" && e.EventType == FileEventType.Added);

        // Cleanup (optional, commented in your style)
         await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_HandlesMultipleLocationsWithDifferentEventTypes()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var folder1 = Path.Combine(tempFolder, "Docs1");
        var folder2 = Path.Combine(tempFolder, "Docs2");
        Directory.CreateDirectory(folder1);
        Directory.CreateDirectory(folder2);

        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs1", folder1, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.UseProcessor<FileLoggerProcessor>();
                })
                .UseLocal("Docs2", folder2, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Initial state for Docs1
        var file1Docs1 = Path.Combine(folder1, "file1.txt");
        var file2Docs1 = Path.Combine(folder1, "file2.txt");
        File.WriteAllText(file1Docs1, "Docs1 Content 1"); // Will be modified
        File.WriteAllText(file2Docs1, "Docs1 Content 2"); // Will be deleted

        // Initial state for Docs2 (same filenames)
        var file1Docs2 = Path.Combine(folder2, "file1.txt");
        var file2Docs2 = Path.Combine(folder2, "file2.txt");
        File.WriteAllText(file1Docs2, "Docs2 Content 1"); // Will remain unchanged
        File.WriteAllText(file2Docs2, "Docs2 Content 2"); // Will be modified

        // Scan 1: Initial state for both locations
        //await sut.StartAsync(CancellationToken.None);
        var scan1Docs1Context = await sut.ScanLocationAsync("Docs1", token: CancellationToken.None);
        var scan1Docs2Context = await sut.ScanLocationAsync("Docs2", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert Scan 1
        var allDocs1Events = await store.GetFileEventsForLocationAsync("Docs1");
        var allDocs2Events = await store.GetFileEventsForLocationAsync("Docs2");
        allDocs1Events.Count.ShouldBe(2); // 2 Added (Scan 1)
        allDocs1Events.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        allDocs2Events.Count.ShouldBe(2); // 2 Added (Scan 1), not Changed
        allDocs2Events.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        // Changes for Docs1
        File.WriteAllText(file1Docs1, "Docs1 Updated Content 1"); // Modify file1
        File.Delete(file2Docs1); // Delete file2
        var file3Docs1 = Path.Combine(folder1, "file3.txt");
        File.WriteAllText(file3Docs1, "Docs1 Content 3"); // Add file3

        // Changes for Docs2
        File.WriteAllText(file2Docs2, "Docs2 Updated Content 2"); // Modify file2
        var file3Docs2 = Path.Combine(folder2, "file3.txt");
        File.WriteAllText(file3Docs2, "Docs2 Content 3"); // Add file3

        // Scan 2: Detect changes for both locations
        var scan2Docs1Context = await sut.ScanLocationAsync("Docs1", token: CancellationToken.None);
        var scan2Docs2Context = await sut.ScanLocationAsync("Docs2", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert Scan 2
        allDocs1Events = await store.GetFileEventsForLocationAsync("Docs1");
        allDocs2Events = await store.GetFileEventsForLocationAsync("Docs2");
        allDocs1Events.Count.ShouldBe(5); // 2 Added (Scan 1) + 1 Changed, 1 Deleted, 1 Added (Scan 2)
        allDocs2Events.Count.ShouldBe(4); // 2 Added (Scan 1) + 1 Changed, 1 Added (Scan 2)

        // Cleanup (optional)
         await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_WithLocalAndInMemoryLocations()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var localFolder = Path.Combine(tempFolder, "LocalDocs");
        Directory.CreateDirectory(localFolder);

        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("LocalDocs", localFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.UseProcessor<FileLoggerProcessor>();
                })
                .UseInMemory("InMemDocs", options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Initial state for LocalDocs (local filesystem)
        var file1Local = Path.Combine(localFolder, "file1.txt");
        var file2Local = Path.Combine(localFolder, "file2.txt");
        File.WriteAllText(file1Local, "Local Content 1");
        File.WriteAllText(file2Local, "Local Content 2");

        // Initial state for InMemDocs (in-memory)
        //var inMemoryProvider = provider.GetServices<ILocationHandler>()
        //    .First(h => h.Options.Name == "InMemDocs").Options.ProcessorConfigs[0].ProcessorType;
        var inMemoryProvider = provider.GetServices<ILocationHandler>()
            .First(h => h.Options.LocationName == "InMemDocs").Provider; // Get the InMemoryFileStorageProvider instance
        await using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("InMem Content 1"));
        await using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("InMem Content 2"));
        await inMemoryProvider.WriteFileAsync("file1.txt", stream1, null, CancellationToken.None);
        await inMemoryProvider.WriteFileAsync("file2.txt", stream2, null, CancellationToken.None);

        // Act: Scan both locations
        //await sut.StartAsync(CancellationToken.None);
        var localScanContext = await sut.ScanLocationAsync("LocalDocs", token: CancellationToken.None);
        var inMemScanContext = await sut.ScanLocationAsync("InMemDocs", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert
        var localEvents = await store.GetFileEventsForLocationAsync("LocalDocs");
        var inMemEvents = await store.GetFileEventsForLocationAsync("InMemDocs");

        localEvents.Count.ShouldBe(2); // 2 Added events for LocalDocs
        localEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        localEvents.Any(e => e.FilePath == "file1.txt").ShouldBeTrue();
        localEvents.Any(e => e.FilePath == "file2.txt").ShouldBeTrue();

        inMemEvents.Count.ShouldBe(2); // 2 Added events for InMemDocs
        inMemEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        inMemEvents.Any(e => e.FilePath == "file1.txt").ShouldBeTrue();
        inMemEvents.Any(e => e.FilePath == "file2.txt").ShouldBeTrue();

        // Cleanup
         await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_PerformanceTestWithLargeTreeStructure()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);

        // Generate a tree structure with many files
        const int depth = 3; // Number of subfolder levels
        const int foldersPerLevel = 5; // Number of subfolders per level
        const int filesPerFolder = 10; // Number of files per folder
        var totalFolders = (int)(Math.Pow(foldersPerLevel, depth + 1) - 1) / (foldersPerLevel - 1); // 156
        var totalFiles = totalFolders * filesPerFolder; // 1560

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        GenerateFolderTree(tempFolder, depth, foldersPerLevel, filesPerFolder);
        stopwatch.Stop();
        var actualFiles = Directory.GetFiles(tempFolder, "*.txt", SearchOption.AllDirectories).Length; // 7800
        output.WriteLine($"Generated {actualFiles} files in {totalFolders} folders in {stopwatch.ElapsedMilliseconds}ms");

        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.RateLimit = RateLimitOptions.MediumSpeed;
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var serviceProvider = services.BuildServiceProvider();
        var sut = serviceProvider.GetRequiredService<IFileMonitoringService>();
        var store = serviceProvider.GetRequiredService<IFileEventStore>();
        //var handler = serviceProvider.GetServices<ILocationHandler>().First(h => h.Options.LocationName == "Docs");

        var progressReports = new List<FileScanProgress>();
        var progress = new Progress<FileScanProgress>(report =>
        {
            progressReports.Add(report);
            output.WriteLine($"progress: {report}");
        });

        // Act: Scan the large tree structure
        //await sut.StartAsync(CancellationToken.None);
        stopwatch.Restart();
        var options = new FileScanOptions
        {
            WaitForProcessing = true,
            Timeout = TimeSpan.FromSeconds(90)
        };
        var scanContext = await sut.ScanLocationAsync("Docs", options, progress, CancellationToken.None);
        //await handler.WaitForQueueEmptyAsync(TimeSpan.FromSeconds(90)); // Wait up to 90s for all events as the event processing (from queue) runs in the background
        stopwatch.Stop();
        output.WriteLine($"Scan detected {scanContext.Events.Count} changes in {stopwatch.ElapsedMilliseconds} ms");

        // Assert
        scanContext.Events.Count.ShouldBe(actualFiles); // Match actual files on disk
        var allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count.ShouldBe(actualFiles);
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        output.WriteLine($"Scanned {allEvents.Count} files in {stopwatch.ElapsedMilliseconds} ms");

        // Verify progress reporting
        progressReports.Count.ShouldBe(11); // 10% intervals (10) + 1 final
        progressReports.Select(r => (int)r.PercentageComplete).ShouldBe([10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 100]);
        progressReports.First().FilesScanned.ShouldBeGreaterThan(0); // Approx 10% of 7800
        progressReports.Last().FilesScanned.ShouldBe(actualFiles); // 7800
        progressReports.Last().TotalFiles.ShouldBe(actualFiles); // 7800
        progressReports.Last().PercentageComplete.ShouldBe(100.0);
        progressReports.All(r => r.ElapsedTime >= TimeSpan.Zero).ShouldBeTrue();
        progressReports.Last().ElapsedTime.ShouldBeGreaterThan(TimeSpan.Zero);

        var scanTimeMs = stopwatch.ElapsedMilliseconds;
        scanTimeMs.ShouldBeLessThan(16000);

        // Cleanup (optional)
         await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_PerformanceTestWithSlowdownAndProgress()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);

        const int depth = 1;
        const int foldersPerLevel = 5;
        const int filesPerFolder = 2; // Reduced to 2 files per folder
        var totalFolders = (int)(Math.Pow(foldersPerLevel, depth + 1) - 1) / (foldersPerLevel - 1); // 6
        var totalFiles = totalFolders * filesPerFolder; // 6 * 2 = 12 (incorrect, actual is 50)

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        GenerateFolderTree(tempFolder, depth, foldersPerLevel, filesPerFolder);
        stopwatch.Stop();
        var actualFiles = Directory.GetFiles(tempFolder, "*.txt", SearchOption.AllDirectories).Length; // Should be 50
        output.WriteLine($"Generated {actualFiles} files in {totalFolders} folders in {stopwatch.Elapsed.TotalSeconds:F2}s");

        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.RateLimit = RateLimitOptions.HighSpeed;
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        var progressReports = new List<FileScanProgress>();
        var progress = new Progress<FileScanProgress>(report =>
        {
            lock (progressReports)
            {
                progressReports.Add(report);
                output.WriteLine($"progress: scanned {report.FilesScanned}/{report.TotalFiles} files ({report.PercentageComplete:F2}%) in {report.ElapsedTime.TotalSeconds:F2}s");
            }
        });

        //await sut.StartAsync(CancellationToken.None);
        stopwatch.Restart();
        var scanOptions = new FileScanOptions
        {
            WaitForProcessing = true,
            Timeout = TimeSpan.FromSeconds(90),
            DelayPerFile = TimeSpan.FromMilliseconds(100) // 100ms per file
        };
        var scanContext = await sut.ScanLocationAsync("Docs", scanOptions, progress, CancellationToken.None);
        stopwatch.Stop();
        output.WriteLine($"Scan detected {scanContext.Events.Count} changes in {stopwatch.Elapsed.TotalSeconds:F2}s");

        var allEvents = await store.GetFileEventsForLocationAsync("Docs");
        output.WriteLine($"Stored {allEvents.Count} events in {stopwatch.Elapsed.TotalSeconds:F2}s");
        allEvents.Count.ShouldBe(actualFiles);
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        progressReports.Count.ShouldBe(11); // 10% intervals (10) + 1 final
        progressReports.Select(r => (int)r.PercentageComplete).ShouldBe([10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 100]);
        progressReports.First().FilesScanned.ShouldBeGreaterThan(0);
        progressReports.Last().FilesScanned.ShouldBe(actualFiles);
        progressReports.Last().TotalFiles.ShouldBe(actualFiles);
        progressReports.Last().PercentageComplete.ShouldBe(100.0);
        progressReports.All(r => r.ElapsedTime >= TimeSpan.Zero).ShouldBeTrue();
        progressReports.Last().ElapsedTime.ShouldBeGreaterThan(TimeSpan.Zero);

        var scanTimeMs = stopwatch.ElapsedMilliseconds;
        scanTimeMs.ShouldBeGreaterThan(4000); // Minimum 5s for 50 files × 100ms

        // Cleanup
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_VerifyProgressReportingCompletion()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseInMemory("InMemDocs", options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.RateLimit = RateLimitOptions.HighSpeed; // 10k/s
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Pre-populate InMemDocs with 100 files
        var inMemProvider = provider.GetServices<ILocationHandler>()
            .First(h => h.Options.LocationName == "InMemDocs")
            .GetType()
            .GetField("inMemoryProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(provider.GetServices<ILocationHandler>().First(h => h.Options.LocationName == "InMemDocs")) as InMemoryFileStorageProvider;

        const int totalFiles = 100;
        for (var i = 0; i < totalFiles; i++)
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"Content {i}"));
            await inMemProvider.WriteFileAsync($"file_{i}.txt", stream, null, CancellationToken.None);
        }

        // Capture progress reports
        var progressReports = new List<FileScanProgress>();
        var progress = new Progress<FileScanProgress>(report =>
        {
            lock (progressReports) // Thread-safe collection
            {
                progressReports.Add(report);
                output.WriteLine($"progress: {report}");
            }
        });

        // Act
        //await sut.StartAsync(CancellationToken.None);
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        var options = new FileScanOptions
        {
            WaitForProcessing = true,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var scanContext = await sut.ScanLocationAsync("InMemDocs", options, progress: progress, CancellationToken.None);
        stopwatch.Stop();
        output.WriteLine($"Scan detected {scanContext.Events.Count} changes in {stopwatch.ElapsedMilliseconds} ms");

        // Assert
        // Verify scan results
        scanContext.Events.Count.ShouldBe(totalFiles); // 100 Added events
        var allEvents = await store.GetFileEventsForLocationAsync("InMemDocs");
        allEvents.Count.ShouldBe(totalFiles); // 100 stored
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        // Verify progress reporting
        //progressReports.Count.ShouldBe(10); // 10% intervals (10 reports) + 1 final
        //progressReports.Select(r => (int)r.PercentageComplete).ShouldBe([10, 20, 30, 40, 50, 60, 70, 80, 90, 100]);
        //progressReports.First().FilesScanned.ShouldBe(10); // Approx 10% of 100
        //progressReports.Last().FilesScanned.ShouldBe(totalFiles); // 100
        progressReports.Last().TotalFiles.ShouldBe(totalFiles); // 100
        //progressReports.Last().PercentageComplete.ShouldBe(100.0);
        progressReports.All(r => r.ElapsedTime >= TimeSpan.Zero).ShouldBeTrue();
        progressReports.Last().ElapsedTime.ShouldBeGreaterThan(TimeSpan.Zero);

        var finalElapsed = progressReports.Last().ElapsedTime.TotalMilliseconds;
        finalElapsed.ShouldBeGreaterThan(0);

        // Cleanup
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_DetectsExistingFiles()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true; // On-demand only
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();
        var sourceFile = Path.Combine(tempFolder, "existing.txt");
        File.WriteAllText(sourceFile, "Existing content");

        // Act
        //await sut.StartAsync(CancellationToken.None);
        await sut.ScanLocationAsync("Docs", token: CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert
        var storedEvents = await store.GetFileEventsAsync("existing.txt");
        storedEvents.ShouldNotBeNull();
        storedEvents.Count().ShouldBe(1);
        var storedEvent = storedEvents.First();
        storedEvent.EventType.ShouldBe(FileEventType.Added);
        storedEvent.FilePath.ShouldBe("existing.txt");

        // Cleanup
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_EventFiltering()
    {
        // Arrange: Create a temporary folder and files
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);

        // Create 5 files
        const int totalFiles = 5;
        for (var i = 0; i < totalFiles; i++)
        {
            File.WriteAllText(Path.Combine(tempFolder, $"file_{i}.txt"), $"Content {i}");
        }

        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.RateLimit = RateLimitOptions.HighSpeed;
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        //await sut.StartAsync(CancellationToken.None);

        // Step 1: Scan with Added filter
        var scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Added)
            .Build();
        var scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 1: Scan detected {scanContext.Events.Count} Added events");

        var allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count.ShouldBe(totalFiles); // 5 Added events
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        // Step 2: Scan with Unchanged filter (files unchanged since last scan)
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Unchanged)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 2: Scan detected {scanContext.Events.Count} Unchanged events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Unchanged).ShouldBe(totalFiles); // 5 Unchanged events
        allEvents.Count.ShouldBe(totalFiles * 2); // 5 Added + 5 Unchanged

        // Step 3: Modify one file and scan with Changed filter
        File.WriteAllText(Path.Combine(tempFolder, "file_0.txt"), "Modified Content");
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Changed)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 3: Scan detected {scanContext.Events.Count} Changed events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Changed).ShouldBe(1); // 1 Changed event
        allEvents.Count.ShouldBe((totalFiles * 2) + 1); // 5 Added + 5 Unchanged + 1 Changed

        // Step 4: Delete one file and scan with Deleted filter
        File.Delete(Path.Combine(tempFolder, "file_0.txt"));
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Deleted)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 4: Scan detected {scanContext.Events.Count} Deleted events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Deleted).ShouldBe(1); // 1 Deleted event
        allEvents.Count.ShouldBe((totalFiles * 2) + 2); // 5 Added + 5 Unchanged + 1 Changed + 1 Deleted

        // Cleanup
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_EventFiltering_WithEFStore()
    {
        // Arrange: Create a temporary folder and files
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);

        // Create 5 files
        const int totalFiles = 5;
        for (var i = 0; i < totalFiles; i++)
        {
            File.WriteAllText(Path.Combine(tempFolder, $"file_{i}.txt"), $"Content {i}");
        }

        // Set up in-memory database
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<TestDbContext>(options =>
                options.UseInMemoryDatabase($"FileMonitoringTest_{Guid.NewGuid()}"));

        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.RateLimit = RateLimitOptions.HighSpeed;
                    options.UseProcessor<FileLoggerProcessor>();
                });
        })
        .WithEntityFrameworkStore<TestDbContext>();

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Ensure the store is the EF store
        store.ShouldBeOfType<EntityFrameworkFileEventStore<TestDbContext>>();

        //await sut.StartAsync(CancellationToken.None);

        // Step 1: Scan with Added filter
        var scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Added)
            .Build();
        var scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 1: Scan detected {scanContext.Events.Count} Added events");

        var allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count.ShouldBe(totalFiles); // 5 Added events
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        // Step 2: Scan with Unchanged filter (files unchanged since last scan)
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Unchanged)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 2: Scan detected {scanContext.Events.Count} Unchanged events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Unchanged).ShouldBe(totalFiles); // 5 Unchanged events
        allEvents.Count.ShouldBe(totalFiles * 2); // 5 Added + 5 Unchanged

        // Step 3: Modify one file and scan with Changed filter
        File.WriteAllText(Path.Combine(tempFolder, "file_0.txt"), "Modified Content");
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Changed)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 3: Scan detected {scanContext.Events.Count} Changed events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Changed).ShouldBe(1); // 1 Changed event
        allEvents.Count.ShouldBe((totalFiles * 2) + 1); // 5 Added + 5 Unchanged + 1 Changed

        // Step 4: Delete one file and scan with Deleted filter
        File.Delete(Path.Combine(tempFolder, "file_0.txt"));
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithEventFilter(FileEventType.Deleted)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, null, CancellationToken.None);
        output.WriteLine($"Step 4: Scan detected {scanContext.Events.Count} Deleted events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Deleted).ShouldBe(1); // 1 Deleted event
        allEvents.Count.ShouldBe((totalFiles * 2) + 2); // 5 Added + 5 Unchanged + 1 Changed + 1 Deleted

        // Cleanup
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_ScanWithAdvancedOptions()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);

        // Create 100 files: 50 .txt, 50 .log
        for (var i = 0; i < 50; i++)
        {
            File.WriteAllText(Path.Combine(tempFolder, $"file_{i}.txt"), $"Content {i}");
            File.WriteAllText(Path.Combine(tempFolder, $"log_{i}.log"), $"Log Content {i}");
        }

        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.*";
                    options.UseOnDemandOnly = true;
                    options.RateLimit = RateLimitOptions.HighSpeed;
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        //await sut.StartAsync(CancellationToken.None);

        // Step 1: Scan with Added filter, .log files only, batch size 5, 5% intervals, skip checksum, max 50 files
        var progressReports = new List<FileScanProgress>();
        var progress = new Progress<FileScanProgress>(report =>
        {
            lock (progressReports)
            {
                progressReports.Add(report);
                output.WriteLine($"Progress: scanned {report.FilesScanned}/{report.TotalFiles} files ({report.PercentageComplete:F2}%) in {report.ElapsedTime.TotalSeconds:F2}s");
            }
        });

        var scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(60))
            .WithDelayPerFile(TimeSpan.FromMilliseconds(10)) // Reduced delay for faster test
            .WithEventFilter(FileEventType.Added)
            .WithFilePathFilter(@".*\.log$")
            //.WithBatchSize(5)
            .WithProgressIntervalPercentage(5)
            //.WithSkipChecksum()
            .WithMaxFilesToScan(50)
            .Build();
        var scanContext = await sut.ScanLocationAsync("Docs", scanOptions, progress, CancellationToken.None);
        output.WriteLine($"Step 1: Scan detected {scanContext.Events.Count} Added events");

        var allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count.ShouldBe(50); // 50 .log files
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        allEvents.All(e => e.FilePath.EndsWith(".log")).ShouldBeTrue();
        progressReports.Count.ShouldBe(21); // 5% intervals (5, 10, ..., 100) = 20 + 1 final
        progressReports.Select(r => (int)r.PercentageComplete).ShouldBe(new[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 100 });

        // Step 2: Scan with Unchanged filter, .log files only, no checksum, max 25 files
        progressReports.Clear();
        scanOptions = FileScanOptionsBuilder.Create()
            .WithWaitForProcessing()
            .WithTimeout(TimeSpan.FromSeconds(30))
            //.WithDelayPerFile(TimeSpan.FromMilliseconds(10))
            .WithEventFilter(FileEventType.Unchanged)
            .WithFilePathFilter(@".*\.log$")
            //.WithBatchSize(5)
            .WithProgressIntervalPercentage(5)
            //.WithSkipChecksum()
            .WithMaxFilesToScan(25)
            .Build();
        scanContext = await sut.ScanLocationAsync("Docs", scanOptions, progress, CancellationToken.None);
        output.WriteLine($"Step 2: Scan detected {scanContext.Events.Count} Unchanged events");

        allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count(e => e.EventType == FileEventType.Unchanged).ShouldBe(25); // 25 .txt files
        allEvents.Count.ShouldBe(75); // 50 Added + 25 Unchanged
        allEvents.Where(e => e.EventType == FileEventType.Unchanged).All(e => e.FilePath.EndsWith(".log")).ShouldBeTrue();
        progressReports.Count.ShouldBe(21); // 5% intervals (5, 10, ..., 100) = 20 + 1 final
        progressReports.Select(r => (int)r.PercentageComplete).ShouldBe([5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 100]);

        await sut.StopAsync(CancellationToken.None);
    }

    private static void GenerateFolderTree(string basePath, int depth, int foldersPerLevel, int filesPerFolder)
    {
        if (depth < 0)
            return;

        for (var i = 0; i < foldersPerLevel; i++)
        {
            var folderName = $"folder_{depth}_{i}";
            var folderPath = Path.Combine(basePath, folderName);
            Directory.CreateDirectory(folderPath);

            // Generate files in this folder
            for (var j = 0; j < filesPerFolder; j++)
            {
                var filePath = Path.Combine(folderPath, $"file_{j}.txt");
                File.WriteAllText(filePath, $"Content {depth}_{i}_{j}");
            }

            // Recursively generate subfolders
            GenerateFolderTree(folderPath, depth - 1, foldersPerLevel, filesPerFolder);
        }
    }
}