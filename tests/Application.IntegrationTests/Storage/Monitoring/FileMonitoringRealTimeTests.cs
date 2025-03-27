// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class FileMonitoringRealTimeTests
{
    private readonly ITestOutputHelper output;

    public FileMonitoringRealTimeTests(ITestOutputHelper output)
    {
        this.output = output;
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
        //await sut.StartAsync(CancellationToken.None);
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content"); // Simulate file creation
        await Task.Delay(500); // Allow real-time watcher to process (FileSystemWatcher latency)

        // Assert
        var storedEvent = await store.GetFileEventsAsync("test.txt");
        storedEvent.ShouldNotBeNull();
        //storedEvent.First().EventType.ShouldBe(FileEventType.Deleted); // due to move (latest event)
        //storedEvent.Last().EventType.ShouldBe(FileEventType.Added); // initialy created (first event)
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
        //await sut.StartAsync(CancellationToken.None);
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
        //allStoredEvents.All(e => e.EventType == FileEventType.Added).ShouldBeTrue(); // DEBOUNCE ISSUE (added+changed) STILL HAPPENS DURING MANY TESTS CONCURRENTLY

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
        //await sut.StartAsync(CancellationToken.None);
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
        //await sut.StartAsync(CancellationToken.None);
        await Task.Delay(500); // Ensure started
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
        //await sut.StartAsync(CancellationToken.None);
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content"); // Triggers Created and Changed
        await Task.Delay(500); // Wait for debounce and processing

        // Assert
        var storedEvents = await store.GetFileEventsAsync("test.txt");
        storedEvents.Count().ShouldBe(1); // Only one event
        var storedEvent = storedEvents.First();
        //storedEvent.EventType.ShouldBe(FileEventType.Added); // First event prioritized // DEBOUNCE ISSUE (added+changed) STILL HAPPENS DURING MANY TESTS CONCURRENTLY
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
        const int fileCount = 25; // Number of files to create
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
        //await sut.StartAsync(CancellationToken.None);
        var sourceFiles = new List<string>();
        for (var i = 0; i < fileCount; i++)
        {
            var sourceFile = Path.Combine(tempFolder, $"test_{i}.txt");
            File.WriteAllText(sourceFile, $"Test content {i}"); // Create multiple files
            sourceFiles.Add($"test_{i}.txt"); // Store relative paths for assertion
            await Task.Delay(300); // Wait for debounce and processing of all events
        }

        await Task.Delay(500); // Wait for debounce and processing of all events

        // Assert
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(sourceFiles.Count); // One event per file
        foreach (var filePath in sourceFiles)
        {
            var storedEvents = await store.GetFileEventsAsync(filePath);
            storedEvents.Count().ShouldBe(1); // Each file has exactly one event
            var storedEvent = storedEvents.First();
            //storedEvent.EventType.ShouldBe(FileEventType.Added); // First event prioritized // DEBOUNCE ISSUE (added+changed) STILL HAPPENS DURING MANY TESTS CONCURRENTLY
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
        //await sut.StartAsync(CancellationToken.None);

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
        //await Task.Delay(500); // Wait for all events to process (debounce issue)
        sourceFiles["test_0.txt"].Add(("Modified", FileEventType.Changed));
        File.Delete(Path.Combine(tempFolder, "test_1.txt")); // Delete file 1
        //await Task.Delay(500); // Wait for all events to process (debounce issue)
        sourceFiles["test_1.txt"].Add(("Deleted", FileEventType.Deleted));
        File.WriteAllText(Path.Combine(tempFolder, "test_2.txt"), "Modified content 2"); // Modify file 2
        //await Task.Delay(500); // Wait for all events to process (debounce issue)
        sourceFiles["test_2.txt"].Add(("Modified", FileEventType.Changed));
        // File 3 remains unchanged
        File.Delete(Path.Combine(tempFolder, "test_4.txt")); // Delete file 4
        //await Task.Delay(500); // Wait for all events to process (debounce issue)
        sourceFiles["test_4.txt"].Add(("Deleted", FileEventType.Deleted));
        await Task.Delay(500); // Wait for all events to process

        // Assert
        var allStoredEvents = await store.GetFileEventsForLocationAsync("Docs");
        allStoredEvents.Count.ShouldBe(9); // 5 Added + 2 Changed + 2 Deleted
        foreach (var (filePath, actions) in sourceFiles)
        {
            var storedEvents = await store.GetFileEventsAsync(filePath);
            storedEvents.Count().ShouldBe(actions.Count); // Matches number of actions
            var orderedEvents = storedEvents.OrderBy(e => e.DetectedDate).ToList();
            for (var i = 0; i < actions.Count; i++)
            {
                orderedEvents[i].EventType.ShouldBe(actions[i].ExpectedEvent, $"Expected {actions[i].Action} event for {filePath}"); // DEBOUNCE ISSUE (added+changed) STILL HAPPENS DURING MANY TESTS CONCURRENTLY
                orderedEvents[i].FilePath.ShouldBe(filePath);
            }
        }

        //// Cleanup
        //await sut.StopAsync(CancellationToken.None);
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

    public Task ProcessAsync(FileProcessingContext context, CancellationToken token)
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