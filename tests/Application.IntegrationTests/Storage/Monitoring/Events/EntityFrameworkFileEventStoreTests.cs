// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using static BridgingIT.DevKit.Application.IntegrationTests.Storage.FileMonitoringConfigurationTests;

public class EntityFrameworkFileEventStoreTests
{
    private TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"FileMonitoringTest_{Guid.NewGuid()}")
            .Options;
        return new TestDbContext(options);
    }

    [Fact]
    public async Task GetFileEventAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);

        // Act
        var result = await store.GetFileEventAsync("nonexistent.txt");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetFileEventAsync_LocationAndFilePath_ReturnsMostRecentEvent()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);
        var (events, locationName, filePath1, _) = FileEventStoreTestHelper.CreateTestEvents();
        foreach (var fileEvent in events)
        {
            await store.StoreEventAsync(fileEvent);
        }

        // Act
        var result = await store.GetFileEventAsync(locationName, filePath1);

        // Assert
        result.ShouldNotBeNull();
        result.EventType.ShouldBe(FileEventType.Changed);
        result.DetectedDate.ShouldBe(events[2].DetectedDate);
    }

    [Fact]
    public async Task GetFileEventsAsync_NonExistentFile_ReturnsEmpty()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);

        // Act
        var result = await store.GetFileEventsAsync("nonexistent.txt");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFileEventsAsync_ExistingFile_ReturnsAllEventsOrdered()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);
        var (events, _, filePath1, _) = FileEventStoreTestHelper.CreateTestEvents();
        foreach (var fileEvent in events)
        {
            await store.StoreEventAsync(fileEvent);
        }

        // Act
        var result = await store.GetFileEventsAsync(filePath1);

        // Assert
        result.Count().ShouldBe(3); // Added, Unchanged, Changed
        result.First().EventType.ShouldBe(FileEventType.Changed); // Ordered by DetectionTime descending
        result.Last().EventType.ShouldBe(FileEventType.Added);
    }

    [Fact]
    public async Task GetFileEventsForLocationAsync_NonExistentLocation_ReturnsEmpty()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);

        // Act
        var result = await store.GetFileEventsForLocationAsync("nonexistent");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFileEventsForLocationAsync_ExistingLocation_ReturnsAllEventsOrdered()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);
        var (events, locationName, _, _) = FileEventStoreTestHelper.CreateTestEvents();
        foreach (var fileEvent in events)
        {
            await store.StoreEventAsync(fileEvent);
        }

        // Act
        var result = await store.GetFileEventsForLocationAsync(locationName);

        // Assert
        result.Count.ShouldBe(5); // All events
        result.First().EventType.ShouldBe(FileEventType.Changed); // Ordered by DetectionTime descending
        result.Last().EventType.ShouldBe(events[0].EventType);
    }

    [Fact]
    public async Task GetPresentFilesAsync_NonExistentLocation_ReturnsEmpty()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);

        // Act
        var result = await store.GetPresentFilesAsync("nonexistent");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPresentFilesAsync_ExistingLocation_ReturnsNonDeletedFiles()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);
        var (events, locationName, filePath1, filePath2) = FileEventStoreTestHelper.CreateTestEvents();
        foreach (var @event in events)
        {
            await store.StoreEventAsync(@event);
        }

        // Act
        var result = await store.GetPresentFilesAsync(locationName);

        // Assert
        result.Count.ShouldBe(1); // Only file_1.txt (file_2.txt is deleted)
        result.ShouldContain(filePath1);
        result.ShouldNotContain(filePath2);
    }

    [Fact]
    public async Task StoreEventAsync_StoresEventCorrectly()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "file_1.txt",
            EventType = FileEventType.Added,
            FileSize = 100,
            LastModifiedDate = DateTimeOffset.UtcNow,
            Checksum = "checksum1",
            DetectedDate = DateTimeOffset.UtcNow
        };

        // Act
        await store.StoreEventAsync(fileEvent);

        // Assert
        var result = await store.GetFileEventAsync(fileEvent.LocationName, fileEvent.FilePath);
        result.ShouldNotBeNull();
        result.EventType.ShouldBe(FileEventType.Added);
        result.FilePath.ShouldBe(fileEvent.FilePath);
    }

    [Fact]
    public async Task StoreProcessingResultAsync_NoOp_DoesNotThrow()
    {
        // Arrange
        var context = this.CreateContext();
        var store = new EntityFrameworkFileEventStore<TestDbContext>(context);
        var result = new FileProcessingResult(); // Assuming ProcessingResult exists

        // Act & Assert
        await store.StoreProcessingResultAsync(result); // Should not throw
    }
}