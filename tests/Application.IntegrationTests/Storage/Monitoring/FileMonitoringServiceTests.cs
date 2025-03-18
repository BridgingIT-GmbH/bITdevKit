// File: BridgingIT.DevKit.Application.FileMonitoring.Tests/FileMonitoringServiceTests.cs
namespace BridgingIT.DevKit.Application.FileMonitoring.Tests;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class FileMonitoringServiceTests
{
    private readonly string tempFolder;

    public FileMonitoringServiceTests()
    {
        this.tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(this.tempFolder);
    }

    //public void Dispose()
    //{
    //    if (Directory.Exists(tempFolder))
    //    {
    //        Directory.Delete(tempFolder, true);
    //    }
    //}

    [Fact]
    public async Task FileMonitoringService_OnDemandScan_DetectsFileChanges()
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
        var handlers = provider.GetServices<LocationHandler>();
        var sourceFile = Path.Combine(tempFolder, "test.txt");
        File.WriteAllText(sourceFile, "Test content");
        await Task.Delay(300); // Allow processing

        // Act
        await sut.StartAsync(CancellationToken.None);
        var scanContext = await sut.ScanLocationAsync("Docs", CancellationToken.None);
        await Task.Delay(100); // Allow processing

        // Assert
        scanContext.DetectedChanges.ShouldHaveSingleItem();
        scanContext.DetectedChanges[0].EventType.ShouldBe(FileEventType.Added);
        scanContext.DetectedChanges[0].FilePath.ShouldBe("test.txt");
        //var movedExists = File.Exists(Path.Combine(tempFolder, "MovedDocs", "test.txt"));
        //movedExists.ShouldBeTrue();
    }

    [Fact]
    public async Task FileMonitoringService_RealTimeWatching_DetectsFileChanges()
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
                    options.UseProcessor<FileMoverProcessor>(config =>
                        config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(tempFolder, "MovedDocs")));
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
        storedEvent.First().EventType.ShouldBe(FileEventType.Deleted); // due to move (latest event)
        storedEvent.Last().EventType.ShouldBe(FileEventType.Added); // initialy created (first event)
        //storedEvent.FilePath.ShouldBe("test.txt");
        //var movedExists = File.Exists(Path.Combine(tempFolder, "MovedDocs", "test.txt"));
        //movedExists.ShouldBeTrue();
    }

    [Fact]
    public void FluentApi_RegistersComponentsCorrectly()
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
                    options.WithProcessorBehavior<LoggingProcessorBehavior>();
                    options.UseProcessor<FileLoggerProcessor>();
                    options.UseProcessor<FileMoverProcessor>(config =>
                        config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(tempFolder, "MovedDocs")))
                        .WithBehavior<RetryProcessorBehavior>();
                });
        });
        var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetService<IFileMonitoringService>();
        var handlers = provider.GetServices<LocationHandler>();
        var store = provider.GetService<IFileEventStore>();

        // Assert
        sut.ShouldNotBeNull();
        handlers.ShouldHaveSingleItem();
        handlers.First().Options.Name.ShouldBe("Docs");
        store.ShouldNotBeNull();
        store.ShouldBeOfType<InMemoryFileEventStore>();
    }
}