// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed class ProfilingWallTimeAccumulator(long started, TimeProvider clock)
{
    private readonly HashSet<ProfilingInvocation> active = [];
    private readonly Dictionary<string, (string Key, long Ticks)> buckets = new(StringComparer.Ordinal);
    private readonly long origin = started;
    private long previous = started;
    private long parallel;
    private long outside;

    public void Start(ProfilingInvocation invocation, long now)
    {
        this.Advance(now);
        this.active.Add(invocation);
    }

    public void Stop(ProfilingInvocation invocation, long now)
    {
        this.Advance(now);
        this.active.Remove(invocation);
    }

    public IReadOnlyList<ProfilingWallTimeBucket> Freeze(long ended)
    {
        this.Advance(ended);
        var values = this.buckets.Values.Select(b => new ProfilingWallTimeBucket
        {
            Kind = ProfilingWallTimeKind.Segment, Key = b.Key, Duration = clock.GetElapsedTime(0, b.Ticks),
        }).ToList();
        if (this.parallel > 0)
        {
            values.Add(new ProfilingWallTimeBucket { Kind = ProfilingWallTimeKind.Parallel, Duration = clock.GetElapsedTime(0, this.parallel) });
        }

        var total = clock.GetElapsedTime(this.origin, ended);
        var covered = values.Aggregate(TimeSpan.Zero, (sum, bucket) => sum + bucket.Duration);
        values.Add(new ProfilingWallTimeBucket { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = total >= covered ? total - covered : TimeSpan.Zero });
        if (covered > total && values.Count > 1)
        {
            var largest = values.FindIndex(b => b.Duration == values.Max(v => v.Duration));
            values[largest] = values[largest] with { Duration = values[largest].Duration - (covered - total) };
        }

        return Array.AsReadOnly(values.ToArray());
    }

    private void Advance(long now)
    {
        var delta = now - this.previous;
        this.previous = now;
        if (this.active.Count == 0)
        {
            this.outside += delta;
        }
        else if (this.active.Count > 1)
        {
            this.parallel += delta;
        }
        else
        {
            var invocation = this.active.First();
            var canonical = ProfilingKeyComparer.Canonicalize(invocation.Summary.Key);
            this.buckets.TryGetValue(canonical, out var current);
            this.buckets[canonical] = (current.Key ?? invocation.Summary.Key, current.Ticks + delta);
        }
    }
}
