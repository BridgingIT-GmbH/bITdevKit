// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.IO;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class FileMonitoringConfigurationTests
{
    [Fact]
    public void FluentApi_AddFileMonitoring_RegistersComponentsCorrectly()
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
        var handlers = provider.GetServices<ILocationHandler>();
        var store = provider.GetService<IFileEventStore>();

        // Assert
        sut.ShouldNotBeNull();
        handlers.ShouldHaveSingleItem();
        handlers.First().Options.LocationName.ShouldBe("Docs");
        store.ShouldNotBeNull();
        store.ShouldBeOfType<InMemoryFileEventStore>();
    }

    [Fact]
    public void FluentApi_AddFileMonitoring_RegistersMultipleLocationsCorrectly()
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
                    options.UseProcessor<FileLoggerProcessor>();
                })
                .UseLocal("Docs2", folder2, options =>
                {
                    options.FilePattern = "*.docx";
                    options.UseOnDemandOnly = true;
                    options.UseProcessor<FileMoverProcessor>(config =>
                        config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(folder2, "MovedDocs")));
                });
        });
        var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetService<IFileMonitoringService>();
        var handlers = provider.GetServices<ILocationHandler>().ToList();
        var store = provider.GetService<IFileEventStore>();

        // Assert
        sut.ShouldNotBeNull();
        handlers.Count.ShouldBe(2); // Two locations registered
        var docs1Handler = handlers.First(h => h.Options.LocationName == "Docs1");
        var docs2Handler = handlers.First(h => h.Options.LocationName == "Docs2");

        docs1Handler.Options.FilePattern.ShouldBe("*.txt");
        docs1Handler.Options.UseOnDemandOnly.ShouldBeFalse(); // Default real-time
        docs1Handler.GetProcessors().Count().ShouldBe(1);
        docs1Handler.GetProcessors().First().ProcessorName.ShouldBe("FileLoggerProcessor");

        docs2Handler.Options.FilePattern.ShouldBe("*.docx");
        docs2Handler.Options.UseOnDemandOnly.ShouldBeTrue();
        docs2Handler.GetProcessors().Count().ShouldBe(1);
        docs2Handler.GetProcessors().First().ProcessorName.ShouldBe("FileMoverProcessor");

        store.ShouldNotBeNull();
        store.ShouldBeOfType<InMemoryFileEventStore>();
    }

    [Fact]
    public void FluentApi_AddFileMonitoring_RegistersBehaviorsCorrectly()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var services = new ServiceCollection().AddLogging();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .WithBehavior<LoggingBehavior>() // Global behavior
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options
                        .WithProcessorBehavior<LoggingProcessorBehavior>() // Location-wide processor behavior
                        .UseProcessor<FileLoggerProcessor>(config => config
                            .WithBehavior<RetryProcessorBehavior>()); // Processor-specific behavior
                });
        });
        var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetService<IFileMonitoringService>();
        var handler = provider.GetServices<ILocationHandler>().First(h => h.Options.LocationName == "Docs");
        var behaviors = provider.GetServices<IMonitoringBehavior>().ToList();
        //var processor = handler.GetProcessors().OfType<FileLoggerProcessor>().First();

        // Assert
        sut.ShouldNotBeNull();
        handler.ShouldNotBeNull();

        // Global behaviors
        behaviors.Count.ShouldBe(1); // One global behavior
        behaviors.First().ShouldBeOfType<LoggingBehavior>();

        // Location processor behaviors
        handler.Options.LocationProcessorBehaviors.Count.ShouldBe(1);
        handler.Options.LocationProcessorBehaviors.First().ShouldBe(typeof(LoggingProcessorBehavior));

        // Processor-specific behaviors
        handler.Options.ProcessorConfigs.Count.ShouldBe(1);
        var processorConfig = handler.Options.ProcessorConfigs.First();
        processorConfig.ProcessorType.ShouldBe(typeof(FileLoggerProcessor));
        processorConfig.BehaviorTypes.Count.ShouldBe(1);
        processorConfig.BehaviorTypes.First().ShouldBe(typeof(RetryProcessorBehavior));
        //processor.Behaviors.Count().ShouldBe(1); // Decorated with RetryProcessorBehavior
        //processor.Behaviors.First().ShouldBeOfType<RetryProcessorBehavior>();
    }

    [Fact]
    public void FluentApi_AddFileMonitoring_RegistersCustomStoreCorrectly()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), $"FileMonitoringTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var services = new ServiceCollection().AddLogging().AddDbContext<TestDbContext>();
        services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .UseLocal("Docs", tempFolder, options =>
                {
                    options.FilePattern = "*.txt";
                    options.UseProcessor<FileLoggerProcessor>();
                });
        })
        .WithEntityFrameworkStore<TestDbContext>(); // Custom store (assuming EF extension exists)
        var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetService<IFileMonitoringService>();
        var handlers = provider.GetServices<ILocationHandler>().ToList();
        var store = provider.GetService<IFileEventStore>();

        // Assert
        sut.ShouldNotBeNull();
        handlers.Count.ShouldBe(1);
        handlers.First().Options.LocationName.ShouldBe("Docs");
        store.ShouldNotBeNull();
        store.ShouldBeOfType<EntityFrameworkFileEventStore<TestDbContext>>(); // Custom store type
    }

    // Dummy TestDbContext for the test (assumes EF extension)
    public class TestDbContext : DbContext, IFileMonitoringContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<FileEventEntity> FileEvents { get; set; }
    }
}