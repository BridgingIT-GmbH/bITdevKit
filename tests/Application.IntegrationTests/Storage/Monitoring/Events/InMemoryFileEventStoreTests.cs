// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;

public class InMemoryFileEventStoreTests
{
    [Fact]
    public async Task GetFileEventAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var store = new InMemoryFileEventStore();

        // Act
        var result = await store.GetFileEventAsync("nonexistent.txt");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetFileEventAsync_LocationAndFilePath_ReturnsMostRecentEvent()
    {
        // Arrange
        var store = new InMemoryFileEventStore();
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
        result.DetectionTime.ShouldBe(events[2].DetectionTime); // Most recent event for filePath1
    }

    [Fact]
    public async Task GetFileEventsAsync_NonExistentFile_ReturnsEmpty()
    {
        // Arrange
        var store = new InMemoryFileEventStore();

        // Act
        var result = await store.GetFileEventsAsync("nonexistent.txt");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFileEventsAsync_ExistingFile_ReturnsAllEventsOrdered()
    {
        // Arrange
        var store = new InMemoryFileEventStore();
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
        var store = new InMemoryFileEventStore();

        // Act
        var result = await store.GetFileEventsForLocationAsync("nonexistent");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFileEventsForLocationAsync_ExistingLocation_ReturnsAllEventsOrdered()
    {
        // Arrange
        var store = new InMemoryFileEventStore();
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
        result.Last().EventType.ShouldBe(events[0].EventType); // First event
    }

    [Fact]
    public async Task GetPresentFilesAsync_NonExistentLocation_ReturnsEmpty()
    {
        // Arrange
        var store = new InMemoryFileEventStore();

        // Act
        var result = await store.GetPresentFilesAsync("nonexistent");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPresentFilesAsync_ExistingLocation_ReturnsNonDeletedFiles()
    {
        // Arrange
        var store = new InMemoryFileEventStore();
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
        var store = new InMemoryFileEventStore();
        var fileEvent = new FileEvent
        {
            LocationName = "Docs",
            FilePath = "file_1.txt",
            EventType = FileEventType.Added,
            FileSize = 100,
            LastModified = DateTimeOffset.UtcNow,
            Checksum = "checksum1",
            DetectionTime = DateTimeOffset.UtcNow
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
        var store = new InMemoryFileEventStore();
        var result = new FileProcessingResult(); // Assuming ProcessingResult exists

        // Act & Assert
        await store.StoreProcessingResultAsync(result); // Should not throw
    }

    [Fact]
    public async Task StoreEventAsync_ConcurrentAccess_HandlesCorrectly()
    {
        // Arrange
        var store = new InMemoryFileEventStore();
        const string locationName = "Docs";
        const string filePath = "file_1.txt";
        var detectionTime = DateTimeOffset.UtcNow;

        var tasks = new List<Task>();
        for (var i = 0; i < 100; i++)
        {
            var fileEvent = new FileEvent
            {
                LocationName = locationName,
                FilePath = filePath,
                EventType = FileEventType.Added,
                FileSize = 100,
                LastModified = DateTimeOffset.UtcNow,
                Checksum = $"checksum{i}",
                DetectionTime = detectionTime.AddSeconds(i)
            };
            tasks.Add(Task.Run(() => store.StoreEventAsync(fileEvent)));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        var events = await store.GetFileEventsAsync(filePath);
        events.Count().ShouldBe(100); // All events stored
        events.First().Checksum.ShouldBe("checksum99"); // Ordered by DetectionTime descending
    }
}

public static class FileEventStoreTestHelper
{
    public static (List<FileEvent> Events, string LocationName, string FilePath1, string FilePath2) CreateTestEvents()
    {
        const string locationName = "Docs";
        const string filePath1 = "file_1.txt";
        const string filePath2 = "file_2.txt";
        var detectionTime = DateTimeOffset.UtcNow;

        var events = new List<FileEvent>
        {
            new FileEvent
            {
                LocationName = locationName,
                FilePath = filePath1,
                EventType = FileEventType.Added,
                FileSize = 100,
                LastModified = DateTimeOffset.UtcNow,
                Checksum = "checksum1",
                DetectionTime = detectionTime
            },
            new FileEvent
            {
                LocationName = locationName,
                FilePath = filePath1,
                EventType = FileEventType.Unchanged,
                FileSize = 100,
                LastModified = DateTimeOffset.UtcNow,
                Checksum = "checksum1",
                DetectionTime = detectionTime.AddSeconds(1)
            },
            new FileEvent
            {
                LocationName = locationName,
                FilePath = filePath1,
                EventType = FileEventType.Changed,
                FileSize = 150,
                LastModified = DateTimeOffset.UtcNow,
                Checksum = "checksum2",
                DetectionTime = detectionTime.AddSeconds(2)
            },
            new FileEvent
            {
                LocationName = locationName,
                FilePath = filePath2,
                EventType = FileEventType.Added,
                FileSize = 200,
                LastModified = DateTimeOffset.UtcNow,
                Checksum = "checksum3",
                DetectionTime = detectionTime
            },
            new FileEvent
            {
                LocationName = locationName,
                FilePath = filePath2,
                EventType = FileEventType.Deleted,
                DetectionTime = detectionTime.AddSeconds(1)
            }
        };

        return (events, locationName, filePath1, filePath2);
    }
}