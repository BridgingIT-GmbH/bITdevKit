// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Text.Json;

public static partial class StorageDimensionsQualification
{
    private static (object Report, bool Passed) AssessMeasuredGates(object runs, object comparisons, object allocations, object storage, long valueCount, string profile, bool smoke, bool scaling)
    {
        var main = JsonSerializer.SerializeToElement(runs, JsonOptions).EnumerateArray().ToArray();
        var baseline = JsonSerializer.SerializeToElement(comparisons, JsonOptions).EnumerateArray().ToArray();
        var allocation = JsonSerializer.SerializeToElement(allocations, JsonOptions).EnumerateArray().ToArray();
        var storageJson = JsonSerializer.SerializeToElement(storage, JsonOptions);
        var selective = main[0].GetProperty("Cases").EnumerateArray().Where(item => item.GetProperty("SelectiveTarget").GetBoolean()).Select(item =>
        {
            var name = item.GetProperty("Name").GetString();
            var median = Median(main.Select(run => ReadDouble(run.GetProperty("Cases").EnumerateArray().Single(value => value.GetProperty("Name").GetString() == name).GetProperty("P95Milliseconds"))));
            return new { Case = name, MedianRunP95Milliseconds = median, Passed = median <= 250 };
        }).ToArray();
        var latency = baseline.Take(1).SelectMany(run => run.GetProperty("Cases").EnumerateArray().ToArray()).Select(item =>
        {
            var name = item.GetProperty("Name").GetString();
            double Measure(bool parent) => Median(baseline.Where(run => run.GetProperty("ParentOnly").GetBoolean() == parent)
                .Select(run => ReadDouble(run.GetProperty("Cases").EnumerateArray().Single(value => value.GetProperty("Name").GetString() == name).GetProperty("P95Milliseconds"))));
            var before = Measure(true);
            var after = Measure(false);
            return new { Case = name, ParentMedianP95Milliseconds = before, DimensionsMedianP95Milliseconds = after, PercentChange = 100 * (after / before - 1), Passed = after <= before * 1.1 };
        }).ToArray();
        var perOperation = allocation.Take(1).SelectMany(run => run.GetProperty("Cases").EnumerateArray().ToArray()).Select(item =>
        {
            var name = item.GetProperty("Name").GetString();
            double Measure(bool parent) => Median(allocation.Where(run => run.GetProperty("ParentOnly").GetBoolean() == parent)
                .Select(run => ReadDouble(run.GetProperty("Cases").EnumerateArray().Single(value => value.GetProperty("Name").GetString() == name).GetProperty("AllocatedBytesPerOperation"))));
            var before = Measure(true);
            var after = Measure(false);
            return new { Case = name, ParentBytesPerOperation = before, DimensionsBytesPerOperation = after, PercentChange = 100 * (after / before - 1), Passed = after <= before * 1.1 };
        }).ToArray();
        var incrementalBytes = UsedBytes(storageJson.GetProperty("Current")) - UsedBytes(storageJson.GetProperty("ParentBaseline"));
        var bytesPerDimension = incrementalBytes / (double)valueCount;
        var coverage = allocation.All(run => run.GetProperty("AttributionCoverage").GetDouble() is >= 0.98 and <= 1.01);
        var delivery = baseline.All(run => run.GetProperty("Cases").EnumerateArray().All(item => item.GetProperty("MissedRequests").GetInt64() == 0 && item.GetProperty("Failures").GetInt64() == 0));
        var passed = selective.All(item => item.Passed) && bytesPerDimension is >= 0 and <= 1024
            && (scaling || (delivery && latency.All(item => item.Passed) && perOperation.All(item => item.Passed) && coverage));
        return (new
        {
            Evaluated = !scaling && !smoke && profile == "reference", ScalingOnly = scaling, MeasuredGateSubsetPassed = smoke || scaling || profile != "reference" ? (bool?)null : passed,
            SelectivePagesAndDeltas = selective, PropertiesAndDimensionlessLatency = latency,
            OfferedLoadDelivered = scaling ? (bool?)null : delivery, PerOperationAllocations = perOperation, AllocationAttributionCovered = scaling ? (bool?)null : coverage,
            IncrementalUsedBytes = incrementalBytes, BytesPerDimension = bytesPerDimension,
            StorageWithinBudget = bytesPerDimension is >= 0 and <= 1024,
            Note = "These measured slices do not certify missing matrix, stress, correctness, or log-work evidence. Smoke results never qualify performance."
        }, smoke || profile != "reference" || passed);

        static long UsedBytes(JsonElement value) => value.TryGetProperty("UsedBtreeBytes", out var bytes) ? bytes.GetInt64() : value.GetProperty("UsedBytes").GetInt64();

        static double ReadDouble(JsonElement value) => value.ValueKind == JsonValueKind.Number ? value.GetDouble() : double.Parse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture);

        static double Median(IEnumerable<double> values)
        {
            var sorted = values.Order().ToArray();
            return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
        }
    }
}
