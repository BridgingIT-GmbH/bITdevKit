// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.FileMonitoring.Tests;

using System;
using System.IO;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

public class FileMonitoringServiceTests
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
        await Task.Delay(300); // Allow processing

        // Act
        await sut.StartAsync(CancellationToken.None);
        var scanContext = await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(100); // Allow processing

        // Assert
        scanContext.Events.ShouldHaveSingleItem();
        scanContext.Events[0].EventType.ShouldBe(FileEventType.Added);
        scanContext.Events[0].FilePath.ShouldBe("test.txt");
        //var movedExists = File.Exists(Path.Combine(tempFolder, "MovedDocs", "test.txt"));
        //movedExists.ShouldBeTrue();
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
        await sut.StartAsync(CancellationToken.None);
        var scanContext = await sut.ScanLocationAsync("Docs", CancellationToken.None);
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
        // await sut.StopAsync(CancellationToken.None);
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
        await sut.StartAsync(CancellationToken.None);
        await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Modify file system state
        var file4 = Path.Combine(tempFolder, "file4.txt");
        File.WriteAllText(file2, "Updated Content 2"); // Modify file2
        File.Delete(file3); // Delete file3
        File.WriteAllText(file4, "Content 4"); // Add file4

        // Act: Second scan to detect changes
        var scanContext = await sut.ScanLocationAsync("Docs", CancellationToken.None);
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
        var file2Ordered = file2Events.OrderBy(e => e.DetectionTime).ToList();
        file2Ordered[0].EventType.ShouldBe(FileEventType.Added);
        file2Ordered[1].EventType.ShouldBe(FileEventType.Changed);

        // File 3: Initial Added + Deleted
        var file3Events = await store.GetFileEventsAsync("file3.txt");
        file3Events.Count().ShouldBe(2);
        var file3Ordered = file3Events.OrderBy(e => e.DetectionTime).ToList();
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
        // await sut.StopAsync(CancellationToken.None);
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
        await sut.StartAsync(CancellationToken.None);
        var scan1Docs1Context = await sut.ScanLocationAsync("Docs1", CancellationToken.None);
        var scan1Docs2Context = await sut.ScanLocationAsync("Docs2", CancellationToken.None);
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
        var scan2Docs1Context = await sut.ScanLocationAsync("Docs1", CancellationToken.None);
        var scan2Docs2Context = await sut.ScanLocationAsync("Docs2", CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert Scan 2
        allDocs1Events = await store.GetFileEventsForLocationAsync("Docs1");
        allDocs2Events = await store.GetFileEventsForLocationAsync("Docs2");
        allDocs1Events.Count.ShouldBe(5); // 2 Added (Scan 1) + 1 Changed, 1 Deleted, 1 Added (Scan 2)
        allDocs2Events.Count.ShouldBe(4); // 2 Added (Scan 1) + 1 Changed, 1 Added (Scan 2)

        // Cleanup (optional)
        // await sut.StopAsync(CancellationToken.None);
    }

    // File: BridgingIT.DevKit.Application.FileMonitoring.Tests/FileMonitoringServiceTests.cs
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
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("InMem Content 1"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("InMem Content 2"));
        await inMemoryProvider.WriteFileAsync("file1.txt", stream1, null, CancellationToken.None);
        await inMemoryProvider.WriteFileAsync("file2.txt", stream2, null, CancellationToken.None);

        // Act: Scan both locations
        await sut.StartAsync(CancellationToken.None);
        var localScanContext = await sut.ScanLocationAsync("LocalDocs", CancellationToken.None);
        var inMemScanContext = await sut.ScanLocationAsync("InMemDocs", CancellationToken.None);
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
        // await sut.StopAsync(CancellationToken.None);
    }

    // File: BridgingIT.DevKit.Application.FileMonitoring.Tests/FileMonitoringServiceTests.cs
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
        var totalFolders = (int)(Math.Pow(foldersPerLevel, depth + 1) - 1) / (foldersPerLevel - 1);
        var totalFiles = totalFolders * filesPerFolder; 

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        GenerateFolderTree(tempFolder, depth, foldersPerLevel, filesPerFolder);
        stopwatch.Stop();
        var actualFiles = Directory.GetFiles(tempFolder, "*.txt", SearchOption.AllDirectories).Length;
        Console.WriteLine($"Generated {actualFiles} files in {totalFolders} folders in {stopwatch.ElapsedMilliseconds}ms");

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

        // Act: Scan the large tree structure
        await sut.StartAsync(CancellationToken.None);
        stopwatch.Restart();
        var scanContext = await sut.ScanLocationAsync("Docs", waitForProcessing: true, timeout: TimeSpan.FromSeconds(90), CancellationToken.None);
        //await handler.WaitForQueueEmptyAsync(TimeSpan.FromSeconds(90)); // Wait up to 90s for all events as the event processing (from queue) runs in the background
        stopwatch.Stop();
        Console.WriteLine($"Scan detected {scanContext.Events.Count} changes"); // Assuming DetectedChanges

        // Assert
        scanContext.Events.Count.ShouldBe(actualFiles); // Match actual files on disk
        var allEvents = await store.GetFileEventsForLocationAsync("Docs");
        allEvents.Count.ShouldBe(actualFiles); 
        allEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        Console.WriteLine($"Scanned {allEvents.Count} files in {stopwatch.ElapsedMilliseconds}ms");

        var scanTimeMs = stopwatch.ElapsedMilliseconds;
        scanTimeMs.ShouldBeLessThan(15000); 

        // Cleanup (optional)
        // await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_Concurrent_RealTimeLocalAndOnDemandInMemory()
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
                    options.RateLimit = RateLimitOptions.MediumSpeed; // 1k/s
                    options.UseProcessor<TestProcessor>();
                })
                .UseInMemory("InMemDocs", options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseOnDemandOnly = true;
                    options.RateLimit = RateLimitOptions.HighSpeed; // 10k/s
                    options.UseProcessor<TestProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Initial state for InMemDocs
        var inMemProvider = provider.GetServices<ILocationHandler>()
            .First(h => h.Options.LocationName == "InMemDocs")
            .GetType()
            .GetField("inMemoryProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(provider.GetServices<ILocationHandler>().First(h => h.Options.LocationName == "InMemDocs")) as InMemoryFileStorageProvider;
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("InMem Content 1"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("InMem Content 2"));
        await inMemProvider.WriteFileAsync("file1.txt", stream1, null, CancellationToken.None);
        await inMemProvider.WriteFileAsync("file2.txt", stream2, null, CancellationToken.None);

        // Act: Start service and run concurrent operations
        await sut.StartAsync(CancellationToken.None);

        // Task 1: Real-time file creations in LocalDocs
        var realTimeTask = Task.Run(async () =>
        {
            File.WriteAllText(Path.Combine(localFolder, "file1.txt"), "Local Content 1");
            await Task.Delay(100); 
            File.WriteAllText(Path.Combine(localFolder, "file2.txt"), "Local Content 2");
            await Task.Delay(100);
        });

        // Task 2: On-demand scan in InMemDocs
        var scanTask = Task.Run(async () =>
        {
            return await sut.ScanLocationAsync("InMemDocs", waitForProcessing: true, timeout: TimeSpan.FromSeconds(30));
        });

        // Wait for both tasks to complete
        await Task.WhenAll(realTimeTask, scanTask);
        //var inMemScanContext = (await scanTask).Events;
        await Task.Delay(500); 

        // Assert
        var localEvents = await store.GetFileEventsForLocationAsync("LocalDocs");
        var inMemEvents = await store.GetFileEventsForLocationAsync("InMemDocs");

        // LocalDocs: Real-time events
        localEvents.Count.ShouldBe(2); // 2 Added events from file creations
        localEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        localEvents.Any(e => e.FilePath == "file1.txt").ShouldBeTrue();
        localEvents.Any(e => e.FilePath == "file2.txt").ShouldBeTrue();

        // InMemDocs: On-demand scan events
        inMemEvents.Count.ShouldBe(2); // 2 Added events from scan
        inMemEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();
        inMemEvents.Any(e => e.FilePath == "file1.txt").ShouldBeTrue();
        inMemEvents.Any(e => e.FilePath == "file2.txt").ShouldBeTrue();
        (await scanTask).Events.Count.ShouldBe(2);

        // Processor invocation
        var localProcessor = provider.GetServices<ILocationHandler>()
            .First(h => h.Options.LocationName == "LocalDocs")
            .GetProcessors().OfType<TestProcessor>().First();
        var inMemProcessor = provider.GetServices<ILocationHandler>()
            .First(h => h.Options.LocationName == "InMemDocs")
            .GetProcessors().OfType<TestProcessor>().First();
        localProcessor.InvocationCount.ShouldBe(2); // 2 real-time events
        inMemProcessor.InvocationCount.ShouldBe(2); // 2 on-demand events

        // Cleanup 
        // await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_OnDemand_DetectsIncrementalChangesWithRenames()
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
        File.WriteAllText(file1, "Content 1"); // Will be renamed
        File.WriteAllText(file2, "Content 2"); // Will be modified
        File.WriteAllText(file3, "Content 3"); // Will be deleted

        // Scan 1: Initial state
        await sut.StartAsync(CancellationToken.None);
        var scan1Context = await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // First change: Rename file1, modify file2, delete file3, add file4
        var file1New = Path.Combine(tempFolder, "file1_renamed.txt");
        File.Move(file1, file1New); // Rename file1 to file1_renamed
        File.WriteAllText(file2, "Updated Content 2"); // Modify file2
        File.Delete(file3); // Delete file3
        var file4 = Path.Combine(tempFolder, "file4.txt");
        File.WriteAllText(file4, "Content 4"); // Add file4

        // Scan 2: Detect first changes
        var scan2Context = await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Second change: Modify file1_renamed, delete file4
        File.WriteAllText(file1New, "Renamed and Modified"); // Modify renamed file
        File.Delete(file4); // Delete file4

        // Scan 3: Detect second changes
        var scan3Context = await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(10); // 3 (Scan 1) + 4 (Scan 2) + 3 (Scan 3)

        // Scan 1: Initial Added events
        scan1Context.Events.Count.ShouldBe(3);
        scan1Context.Events.ShouldContain(e => e.FilePath == "file1.txt" && e.EventType == FileEventType.Added);
        scan1Context.Events.ShouldContain(e => e.FilePath == "file2.txt" && e.EventType == FileEventType.Added);
        scan1Context.Events.ShouldContain(e => e.FilePath == "file3.txt" && e.EventType == FileEventType.Added);

        // Scan 2: Rename (Deleted + Added), Modified, Deleted, Added
        scan2Context.Events.Count.ShouldBe(5);
        scan2Context.Events.ShouldContain(e => e.FilePath == "file1.txt" && e.EventType == FileEventType.Deleted);
        scan2Context.Events.ShouldContain(e => e.FilePath == "file1_renamed.txt" && e.EventType == FileEventType.Added);
        scan2Context.Events.ShouldContain(e => e.FilePath == "file2.txt" && e.EventType == FileEventType.Changed);
        scan2Context.Events.ShouldContain(e => e.FilePath == "file3.txt" && e.EventType == FileEventType.Deleted);
        scan2Context.Events.ShouldContain(e => e.FilePath == "file4.txt" && e.EventType == FileEventType.Added);

        // Scan 3: Modified, Deleted
        scan3Context.Events.Count.ShouldBe(2);
        scan3Context.Events.ShouldContain(e => e.FilePath == "file1_renamed.txt" && e.EventType == FileEventType.Changed);
        scan3Context.Events.ShouldContain(e => e.FilePath == "file4.txt" && e.EventType == FileEventType.Deleted);

        // Stored events per file
        var file1Events = await store.GetFileEventsAsync("file1.txt");
        file1Events.Count().ShouldBe(2); // Added, Deleted
        var file1NewEvents = await store.GetFileEventsAsync("file1_renamed.txt");
        file1NewEvents.Count().ShouldBe(2); // Added, Changed
        var file2Events = await store.GetFileEventsAsync("file2.txt");
        file2Events.Count().ShouldBe(2); // Added, Changed
        var file3Events = await store.GetFileEventsAsync("file3.txt");
        file3Events.Count().ShouldBe(2); // Added, Deleted
        var file4Events = await store.GetFileEventsAsync("file4.txt");
        file4Events.Count().ShouldBe(2); // Added, Deleted

        // Cleanup 
        // await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_DetectsFileChanges()
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
                    // Real-time watching enabled (default, not UseOnDemandOnly)
                    options.UseProcessor<FileLoggerProcessor>();
                    //options.UseProcessor<FileLoggerProcessor>();
                    //options.UseProcessor<FileLoggerProcessor>();
                    //options.UseProcessor<FileMoverProcessor>(config =>
                    //    config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(tempFolder, "MovedDocs")));
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Act
        await sut.StartAsync(CancellationToken.None);
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content"); // Simulate file creation
        await Task.Delay(500); // Allow real-time watcher to process (FileSystemWatcher latency)

        // Assert
        var storedEvent = await store.GetFileEventsAsync("test.txt");
        storedEvent.ShouldNotBeNull();
        //storedEvent.First().EventType.ShouldBe(FileEventType.Deleted); // due to move (latest event)
        storedEvent.Last().EventType.ShouldBe(FileEventType.Added); // initialy created (first event)
        //storedEvent.FilePath.ShouldBe("test.txt");
        //var movedExists = File.Exists(Path.Combine(tempFolder, "MovedDocs", "test.txt"));
        //movedExists.ShouldBeTrue();
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_CallsProcessorForEachEvent()
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
                    // Real-time watching enabled (default, no UseOnDemandOnly)
                    options.UseProcessor<TestProcessor>(); // Custom processor
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Act: Start real-time watching and simulate file events
        await sut.StartAsync(CancellationToken.None);
        var files = new[]
        {
            Path.Combine(tempFolder, "file1.txt"),
            Path.Combine(tempFolder, "file2.txt"),
            Path.Combine(tempFolder, "file3.txt")
        };
        File.WriteAllText(files[0], "Content 1"); // Triggers Added event
        File.WriteAllText(files[1], "Content 2"); // Triggers Added event
        File.WriteAllText(files[2], "Content 3"); // Triggers Added event
        await Task.Delay(1000); // Allow FileSystemWatcher and processor to handle events (includes debounce)

        // Retrieve the TestProcessor instance from the handler
        var handler = provider.GetServices<ILocationHandler>().First(h => h.Options.LocationName == "Docs");
        var processor = handler.GetProcessors()
            .OfType<TestProcessor>().First(); // Assumes TestProcessor is unique

        // Assert
        processor.InvocationCount.ShouldBe(3); // Processor called for each Added event
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(3); // 3 Added events stored (debouncing ensures one per file)
        allStoredEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue();

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_MultipleLocations_ProcessIndependently()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(tempFolder, "1"));
        Directory.CreateDirectory(Path.Combine(tempFolder, "2"));
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs1", Path.Combine(tempFolder, "1"), options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseProcessor<FileLoggerProcessor>();
                    //options.UseProcessor<FileMoverProcessor>(config =>
                    //    config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(tempFolder, "MovedDocs1")));
                })
                .UseLocal("Docs2", Path.Combine(tempFolder, "2"), options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseProcessor<FileLoggerProcessor>();
                    //options.UseProcessor<FileMoverProcessor>(config =>
                    //    config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(tempFolder, "MovedDocs2")));
                });
        });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Act
        await sut.StartAsync(CancellationToken.None);
        var file1 = Path.Combine(tempFolder, "1", "test1.txt");
        var file2 = Path.Combine(tempFolder, "2", "test2.txt");
        File.WriteAllText(file1, "Content1");
        File.WriteAllText(file2, "Content2");
        await Task.Delay(1000); // Allow real-time watcher to process (FileSystemWatcher latency)
        //await sut.StopAsync(CancellationToken.None);

        // Assert
        var storedEvents = await store.GetFileEventsAsync("test1.txt");
        var events1 = await store.GetFileEventsForLocationAsync("Docs1");
        var events2 = await store.GetFileEventsForLocationAsync("Docs2");
        events1.ShouldNotBeNull();
        events1.ShouldNotBeEmpty();
        events2.ShouldNotBeNull();
        events2.ShouldNotBeEmpty();
        //File.Exists(Path.Combine(Path.Combine(tempFolder, "Docs1"), "MovedDocs1", "test1.txt")).ShouldBeTrue();
        //File.Exists(Path.Combine(Path.Combine(tempFolder, "Docs2"), "MovedDocs2", "test2.txt")).ShouldBeTrue();
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
        await sut.StartAsync(CancellationToken.None);
        await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(500); // Allow processing

        // Assert
        var storedEvents = await store.GetFileEventsAsync("existing.txt");
        storedEvents.ShouldNotBeNull();
        storedEvents.Count().ShouldBe(1);
        var storedEvent = storedEvents.First();
        storedEvent.EventType.ShouldBe(FileEventType.Added);
        storedEvent.FilePath.ShouldBe("existing.txt");

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_PauseResume_ControlsRealTimeWatching()
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
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Act - Start and Pause Watching
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Ensure started
        await sut.PauseLocationAsync("Docs");
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content"); // Event during pause
        await Task.Delay(500); // Wait, should not capture

        // Assert - No events during pause
        var storedEventsPaused = await store.GetFileEventsAsync("test.txt");
        storedEventsPaused.ShouldBeEmpty();

        // Act - Resume Watching
        await sut.ResumeLocationAsync("Docs");
        File.WriteAllText(sourceFile, "Updated content"); // New event after resume
        await Task.Delay(500); // Allow processing

        // Assert - Event captured after resume
        var storedEventsResumed = await store.GetFileEventsAsync("test.txt");
        storedEventsResumed.ShouldNotBeNull();
        storedEventsResumed.Count().ShouldBe(1);
        storedEventsResumed.First().EventType.ShouldBe(FileEventType.Changed);

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_PrioritizesFirstEventAsAdded()
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
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Act
        await sut.StartAsync(CancellationToken.None);
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content"); // Triggers Created and Changed
        await Task.Delay(500); // Wait for debounce and processing

        // Assert
        var storedEvents = await store.GetFileEventsAsync("test.txt");
        storedEvents.Count().ShouldBe(1); // Only one event
        var storedEvent = storedEvents.First();
        storedEvent.EventType.ShouldBe(FileEventType.Added); // First event prioritized
        storedEvent.FilePath.ShouldBe("test.txt");

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_HandlesMultipleFileCreations()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        const int fileCount = 100; // Number of files to create
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();

        // Act
        await sut.StartAsync(CancellationToken.None);
        var sourceFiles = new List<string>();
        for (var i = 0; i < fileCount; i++)
        {
            var sourceFile = Path.Combine(tempFolder, $"test_{i}.txt");
            File.WriteAllText(sourceFile, $"Test content {i}"); // Create multiple files
            sourceFiles.Add($"test_{i}.txt"); // Store relative paths for assertion
        }
        await Task.Delay(1000); // Wait for debounce and processing of all events

        // Assert
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(fileCount); // One event per file
        foreach (var filePath in sourceFiles)
        {
            var storedEvents = await store.GetFileEventsAsync(filePath);
            storedEvents.Count().ShouldBe(1); // Each file has exactly one event
            var storedEvent = storedEvents.First();
            storedEvent.EventType.ShouldBe(FileEventType.Added); // First event prioritized
            storedEvent.FilePath.ShouldBe(filePath);
        }

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FileMonitoringService_RealTime_HandlesMixedActionsOnMultipleFiles()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        const int fileCount = 5; // Small set for clarity, can increase
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseProcessor<FileLoggerProcessor>();
                });
        });
        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IFileMonitoringService>();
        var store = provider.GetRequiredService<IFileEventStore>();
        var sourceFiles = new Dictionary<string, List<(string Action, FileEventType ExpectedEvent)>>();

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Create 5 files
        for (var i = 0; i < fileCount; i++)
        {
            var filePath = Path.Combine(tempFolder, $"test_{i}.txt");
            File.WriteAllText(filePath, $"Initial content {i}");
            sourceFiles[$"test_{i}.txt"] = [("Created", FileEventType.Added)];
        }
        await Task.Delay(500); // Wait for initial creation events

        // Mixed actions
        File.WriteAllText(Path.Combine(tempFolder, "test_0.txt"), "Modified content 0"); // Modify file 0
        //await Task.Delay(100); // Wait for all events to process (debounce issue)
        sourceFiles["test_0.txt"].Add(("Modified", FileEventType.Changed));
        File.Delete(Path.Combine(tempFolder, "test_1.txt")); // Delete file 1
        //await Task.Delay(100); // Wait for all events to process (debounce issue)
        sourceFiles["test_1.txt"].Add(("Deleted", FileEventType.Deleted));
        File.WriteAllText(Path.Combine(tempFolder, "test_2.txt"), "Modified content 2"); // Modify file 2
        //await Task.Delay(100); // Wait for all events to process (debounce issue)
        sourceFiles["test_2.txt"].Add(("Modified", FileEventType.Changed));
        // File 3 remains unchanged
        File.Delete(Path.Combine(tempFolder, "test_4.txt")); // Delete file 4
        //await Task.Delay(100); // Wait for all events to process (debounce issue)
        sourceFiles["test_4.txt"].Add(("Deleted", FileEventType.Deleted));
        await Task.Delay(500); // Wait for all events to process

        // Assert
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(9); // 5 Added + 2 Changed + 2 Deleted
        foreach (var (filePath, actions) in sourceFiles)
        {
            var storedEvents = await store.GetFileEventsAsync(filePath);
            storedEvents.Count().ShouldBe(actions.Count); // Matches number of actions
            var orderedEvents = storedEvents.OrderBy(e => e.DetectionTime).ToList();
            for (var i = 0; i < actions.Count; i++)
            {
                orderedEvents[i].EventType.ShouldBe(actions[i].ExpectedEvent, $"Expected {actions[i].Action} event for {filePath}");
                orderedEvents[i].FilePath.ShouldBe(filePath);
            }
        }

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
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

public class TestProcessor : IFileEventProcessor
{
    private readonly ILogger<TestProcessor> logger;
    private int invocationCount;

    public TestProcessor(ILogger<TestProcessor> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProcessorName => nameof(TestProcessor);

    public bool IsEnabled { get; set; } = true;

    public IEnumerable<IProcessorBehavior> Behaviors => Array.Empty<IProcessorBehavior>();

    public int InvocationCount => this.invocationCount; // Public property to check calls

    public Task ProcessAsync(ProcessingContext context, CancellationToken token)
    {
        if (context == null || context.FileEvent == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        token.ThrowIfCancellationRequested();

        Interlocked.Increment(ref this.invocationCount); // Thread-safe increment
        this.logger.LogInformation($"TestProcessor invoked for event: {context.FileEvent.FilePath}, Type: {context.FileEvent.EventType}");

        return Task.CompletedTask;
    }
}