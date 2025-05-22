// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class FileProcessorTests
{
    private readonly IServiceProvider serviceProvider;
    private readonly ITestOutputHelper output;

    public FileProcessorTests(ITestOutputHelper output)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<IFileStorageProvider>(sp => new InMemoryFileStorageProvider("TestInMemory"))
            .AddScoped<IFileEventStore, InMemoryFileEventStore>();
        this.serviceProvider = services.BuildServiceProvider();
        this.output = output;
    }

    [Fact]
    public async Task FileLoggerProcessor_LogsEventDetails()
    {
        // Arrange
        var logger = new TestLogger<FileLoggerProcessor>();
        var sut = new FileLoggerProcessor(logger);
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "test.txt",
            EventType = FileEventType.Added,
            FileSize = 100,
            LastModifiedDate = DateTimeOffset.UtcNow,
            Checksum = "abc123"
        };
        var context = new FileProcessingContext(fileEvent);

        // Act
        await sut.ProcessAsync(context, CancellationToken.None);

        // Assert
        logger.Messages.ShouldContain(m => m.Contains("file event processed") && m.Contains("test.txt"));
        logger.Messages.ShouldContain(m => m.Contains("Location=Docs"));
        logger.Messages.ShouldContain(m => m.Contains("Type=Added"));
    }

    [Fact]
    public async Task FileMoverProcessor_MovesFileToDestination()
    {
        // Arrange
        var logger = new NullLogger<FileMoverProcessor>();
        var sut = new FileMoverProcessor(logger) { DestinationRoot = "MovedDocs" };
        var provider = this.serviceProvider.GetRequiredService<IFileStorageProvider>();
        await provider.WriteFileAsync("test.txt", new MemoryStream(new byte[100]), null, CancellationToken.None);
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "test.txt",
            EventType = FileEventType.Added,
            FileSize = 100,
            LastModifiedDate = DateTimeOffset.UtcNow,
            Checksum = "abc123"
        };
        var context = new FileProcessingContext(fileEvent);
        context.SetItem("StorageProvider", provider);

        // Act
        await sut.ProcessAsync(context, CancellationToken.None);

        // Assert
        var existsInSource = await provider.FileExistsAsync("test.txt", null, CancellationToken.None);
        var existsInDest = await provider.FileExistsAsync("MovedDocs/test.txt", null, CancellationToken.None);
        existsInSource.ShouldBeFailure();
        existsInDest.ShouldBeSuccess();
    }

    [Fact]
    public async Task RetryProcessorBehavior_RetriesOnFailure()
    {
        // Arrange
        var logger = new TestLogger<RetryProcessorBehavior>();
        var sut = new RetryProcessorBehavior(logger, maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(50));
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "test.txt",
            EventType = FileEventType.Added
        };
        var context = new FileProcessingContext(fileEvent);
        var failureResult = Result<bool>.Failure().WithError(new ExceptionError(new Exception("Processing failed")));

        // Act & Assert First Attempt
        await Should.ThrowAsync<RetryException>(() => sut.AfterProcessAsync(context, failureResult, CancellationToken.None));
        context.GetItem<int>("RetryAttempt").ShouldBe(1);

        // Act & Assert Second Attempt
        await Task.Delay(75); // Wait for first delay (50ms)
        await Should.ThrowAsync<RetryException>(() => sut.AfterProcessAsync(context, failureResult, CancellationToken.None));
        context.GetItem<int>("RetryAttempt").ShouldBe(2);

        // Act & Assert Third Attempt (Max Reached)
        await Task.Delay(125); // Wait for second delay (100ms)
        await sut.AfterProcessAsync(context, failureResult, CancellationToken.None);
        context.GetItem<int>("RetryAttempt").ShouldBe(2); // No further increment
        logger.Messages.ShouldContain(m => m.Contains("Max retry attempts (3) reached"));
    }

    [Fact]
    public async Task LoggingProcessorBehavior_LogsBeforeAndAfterProcessing()
    {
        // Arrange
        var logger = new TestLogger<LoggingProcessorBehavior>();
        var sut = new LoggingProcessorBehavior(logger);
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "test.txt",
            EventType = FileEventType.Added
        };
        var context = new FileProcessingContext(fileEvent);
        var successResult = Result<bool>.Success(true);
        var failureResult = Result<bool>.Failure().WithError(new ExceptionError(new Exception("Failed")));

        // Act - Success Case
        await sut.BeforeProcessAsync(context, CancellationToken.None);
        await sut.AfterProcessAsync(context, successResult, CancellationToken.None);

        // Assert - Success Case
        logger.Messages.ShouldContain(m => m.Contains("starting processing") && m.Contains("test.txt"));
        logger.Messages.ShouldContain(m => m.Contains("completed processing") && m.Contains("test.txt"));

        // Act - Failure Case
        logger.Messages.Clear();
        await sut.BeforeProcessAsync(context, CancellationToken.None);
        await sut.AfterProcessAsync(context, failureResult, CancellationToken.None);

        // Assert - Failure Case
        logger.Messages.ShouldContain(m => m.Contains("starting processing") && m.Contains("test.txt"));
        logger.Messages.ShouldContain(m => m.Contains("failed processing") && m.Contains("Failed"));
    }

    [Fact]
    public async Task LocationHandler_ProcessesEventWithChain()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var logger = new NullLogger<LocalLocationHandler>();
        var provider = new LocalFileStorageProvider("Docs1", tempFolder);
        var store = this.serviceProvider.GetRequiredService<IFileEventStore>();
        var options = new LocationOptions("Docs")
        {
            FileFilter = "*.txt",
            UseOnDemandOnly = true,
            ProcessorConfigs =
            {
                new ProcessorConfiguration { ProcessorType = typeof(FileLoggerProcessor) },
                new ProcessorConfiguration
                {
                    ProcessorType = typeof(FileMoverProcessor),
                    Configure = p => ((FileMoverProcessor)p).DestinationRoot = "MovedDocs",
                    BehaviorTypes = { typeof(RetryProcessorBehavior) }
                }
            }
        };
        var sut = new LocalLocationHandler(logger, provider, store, options, this.serviceProvider);
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "test.txt",
            EventType = FileEventType.Added,
            FileSize = 100,
            LastModifiedDate = DateTimeOffset.UtcNow,
            Checksum = "abc123"
        };
        await provider.WriteFileAsync("test.txt", new MemoryStream(new byte[100]), null, CancellationToken.None);

        // Act
        //await sut.StartAsync(CancellationToken.None);
        var eventQueueField = typeof(LocalLocationHandler).GetField("eventQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var eventQueue = eventQueueField.GetValue(sut) as BlockingCollection<FileEvent>;
        eventQueue.Add(fileEvent); // Simulate event
        await Task.Delay(500); // Allow processing
        //await sut.StopAsync(CancellationToken.None);

        // Assert
        var movedExists = await provider.FileExistsAsync("MovedDocs/test.txt", null, CancellationToken.None);
        movedExists.ShouldBeSuccess();
        var storedEvent = await store.GetFileEventAsync("test.txt");
        storedEvent.ShouldNotBeNull();
        storedEvent.FilePath.ShouldBe("test.txt");
    }

    [Fact]
    public void FluentApi_RegistersComponentsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                //.WithBehavior<LoggingBehavior>()
                .UseLocal("Docs", "C:\\Docs", options =>
                {
                    options.FileFilter = "*.txt";
                    options.WithProcessorBehavior<LoggingProcessorBehavior>();
                    options.UseProcessor<FileLoggerProcessor>();
                    options.UseProcessor<FileMoverProcessor>(config =>
                        config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = "C:\\MovedDocs"))
                        .WithBehavior<RetryProcessorBehavior>();
                });
        });
        var provider = services.BuildServiceProvider();

        // Act
        var monitoringService = provider.GetService<IFileMonitoringService>();
        var handlers = provider.GetServices<ILocationHandler>();
        //var behaviors = provider.GetServices<IMonitoringBehavior>();
        var store = provider.GetService<IFileEventStore>();

        // Assert
        monitoringService.ShouldNotBeNull();
        handlers.ShouldHaveSingleItem();
        handlers.First().Options.LocationName.ShouldBe("Docs");
        //behaviors.ShouldContain(b => b.GetType() == typeof(LoggingProcessorBehavior));
        store.ShouldNotBeNull();
        store.ShouldBeOfType<InMemoryFileEventStore>();
    }
}

// Simple in-memory logger for testing
public class TestLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        this.Messages.Add(formatter(state, exception));
    }
}