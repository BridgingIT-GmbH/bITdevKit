// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Broadcasting;

using BridgingIT.DevKit.Common.UnitTests.Utilities;

public class BroadcastingTelemetryTests
{
    [Fact]
    public void Metrics_ImmediateOutcomes_UseLowCardinalitySeries()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        using var metrics = new MetricsService(meterFactory);
        var type = Metrics.NormalizeTypeName(typeof(TelemetryBroadcast));

        // Act
        BroadcastingMetrics.RecordPublication(metrics, typeof(TelemetryBroadcast), 3);
        BroadcastingMetrics.RecordDelivery(
            metrics,
            typeof(TelemetryBroadcast),
            BroadcastDeliveryOutcome.Unsupported,
            TimeSpan.FromMilliseconds(12)
        );
        BroadcastingMetrics.RecordReceiver(
            metrics,
            typeof(TelemetryBroadcast).FullName,
            BroadcastDeliveryOutcome.AlreadyProcessed
        );
        BroadcastingMetrics.RecordRegistration(metrics, "registered");
        BroadcastingMetrics.RecordStaleRemoval(metrics, 2);

        // Assert
        recorder.CounterSum($"broadcasting_publish_{type}").ShouldBe(1);
        recorder.CounterSum($"broadcasting_targets_{type}").ShouldBe(3);
        recorder
            .CounterSum(Metrics.Series("broadcasting_delivery", type, "Unsupported"))
            .ShouldBe(1);
        recorder
            .CounterSum(
                Metrics.Series(
                    "broadcasting_receiver",
                    typeof(TelemetryBroadcast).FullName,
                    "AlreadyProcessed"
                )
            )
            .ShouldBe(1);
        recorder.CounterSum("broadcasting_registration_registered").ShouldBe(1);
        recorder.CounterSum("broadcasting_stale_removal").ShouldBe(2);
        recorder
            .HistogramCount(
                Metrics.Series("broadcasting_delivery_duration", type, "Unsupported")
            )
            .ShouldBe(1);
    }

    private sealed record TelemetryBroadcast;
}
