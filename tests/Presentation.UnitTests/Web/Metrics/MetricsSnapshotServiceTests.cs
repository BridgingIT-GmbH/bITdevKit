// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Metrics;

using BridgingIT.DevKit.Presentation.Web;

public class MetricsSnapshotServiceTests
{
    [Fact]
    public void GetSnapshot_WithBroadcastingMetrics_ClassifiesBroadcastingFamily()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var publishedName = $"broadcasting_publish_snapshot_{suffix}";
        var acceptedName = $"broadcasting_receiver_snapshot_{suffix}_accepted";
        var durationName = $"broadcasting_delivery_duration_snapshot_{suffix}_accepted";
        using var snapshotService = new MetricsSnapshotService();
        using var metricsService = new MetricsService();

        // Act
        metricsService.AddCounter(publishedName, 2);
        metricsService.AddCounter(acceptedName, 3);
        metricsService.RecordHistogram(durationName, 4, "ms");
        var snapshot = snapshotService.GetSnapshot();

        // Assert
        var feature = snapshot.Features["broadcasting"];
        feature.Counters[publishedName].ShouldBe(2);
        feature.Counters[acceptedName].ShouldBe(3);
        feature.Durations[durationName].Count.ShouldBe(1);
        feature.Durations[durationName].Average.ShouldBe(4);
    }

    [Fact]
    public void GetSnapshot_WithStorageAndCompositionMetrics_ClassifiesAllFamilies()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var blobName = $"blobstorage_snapshot_{suffix}";
        var fileName = $"filestorage_snapshot_{suffix}";
        var documentName = $"document.snapshot.{suffix}";
        var permalinkName = $"bdk.storage.permalinks.snapshot.{suffix}";
        var compositionName = $"composition_snapshot_{suffix}";
        using var snapshotService = new MetricsSnapshotService();
        using var metricsService = new MetricsService();

        // Act
        metricsService.AddCounter(blobName, 2);
        metricsService.AddCounter(fileName, 3);
        metricsService.AddCounter(documentName, 4);
        metricsService.AddCounter(permalinkName, 5);
        metricsService.AddCounter(compositionName, 6);
        var snapshot = snapshotService.GetSnapshot();

        // Assert
        snapshot.Features["blobstorage"].Counters[blobName].ShouldBe(2);
        snapshot.Features["filestorage"].Counters[fileName].ShouldBe(3);
        snapshot.Features["documentstorage"].Counters[documentName].ShouldBe(4);
        snapshot.Features["storagepermalinks"].Counters[permalinkName].ShouldBe(5);
        snapshot.Features["composition"].Counters[compositionName].ShouldBe(6);
    }

    [Fact]
    public void GetSnapshot_WhenGaugeChanges_ReportsLatestValueWithoutAccumulating()
    {
        // Arrange
        var gaugeName = $"bdk.storage.permalinks.sync.queue.depth.{Guid.NewGuid():N}";
        using var snapshotService = new MetricsSnapshotService();
        using var metricsService = new MetricsService();

        // Act
        metricsService.SetGauge(gaugeName, 4);
        var first = snapshotService.GetSnapshot();
        metricsService.SetGauge(gaugeName, 7);
        var second = snapshotService.GetSnapshot();

        // Assert
        first.Features["storagepermalinks"].Current[gaugeName].ShouldBe(4);
        second.Features["storagepermalinks"].Current[gaugeName].ShouldBe(7);
    }

    [Fact]
    public void GetSnapshot_WithStorageSummarySeries_ComputesFeatureTotals()
    {
        // Arrange
        using var snapshotService = new MetricsSnapshotService();
        using var metricsService = new MetricsService();

        // Act
        metricsService.AddCounter("blobstorage_operations", 2);
        metricsService.AddCounter("blobstorage_operation_failures");
        metricsService.AddUpDownCounter("blobstorage_uploads_active", 3);
        var snapshot = snapshotService.GetSnapshot();

        // Assert
        var feature = snapshot.Features["blobstorage"];
        feature.SuccessTotal.ShouldBe(2);
        feature.FailureTotal.ShouldBe(1);
        feature.CurrentTotal.ShouldBe(3);
        feature.TopFailures.ShouldContain(metric =>
            metric.Name == "blobstorage_operation_failures" && metric.Value == 1);
    }
}
