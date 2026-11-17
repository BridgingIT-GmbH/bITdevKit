// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Presentation.Benchmarks;
using BridgingIT.DevKit.Presentation.Benchmarks.Caching;
using BenchmarkDotNet.Running;
using BridgingIT.DevKit.Presentation.Benchmarks.AppState;

if (args.Length > 0 && args[0] == "appstate-allocations")
{
    await AppStateAllocationExperiments.RunAsync(args[1..]);
}
else if (args.Length > 0 && args[0] == "appstate")
{
    BenchmarkSwitcher.FromAssembly(typeof(AppStateSnapshotBenchmarks).Assembly).Run(args[1..]);
}
else if (args.Length > 0 && string.Equals(args[0], "caching", StringComparison.OrdinalIgnoreCase))
{
    await CacheHttpExperiments.RunAsync(CacheHttpExperimentOptions.Parse(args[1..]));
}
else
{
    await StreamingExperiments.RunAsync(args);
}
