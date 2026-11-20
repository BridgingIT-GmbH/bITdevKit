// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

public static partial class StorageDimensionsQualification
{
    private static object CaptureGCPauses()
    {
        var blocking = GC.GetGCMemoryInfo(GCKind.FullBlocking);
        var background = GC.GetGCMemoryInfo(GCKind.Background);
        return new
        {
            TotalPauseMilliseconds = GC.GetTotalPauseDuration().TotalMilliseconds,
            BlockingCollectionIndex = blocking.Index,
            LastBlockingPauseMilliseconds = blocking.PauseDurations.ToArray().Select(pause => pause.TotalMilliseconds).DefaultIfEmpty().Max(),
            BackgroundCollectionIndex = background.Index,
            LastBackgroundPauseMilliseconds = background.PauseDurations.ToArray().Select(pause => pause.TotalMilliseconds).DefaultIfEmpty().Max(),
            Note = "Captured outside the timed interval; last pauses are attributed to an interval only when their collection index changes."
        };
    }
}
