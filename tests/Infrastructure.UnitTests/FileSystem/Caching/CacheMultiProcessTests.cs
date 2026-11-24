// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

/// <summary>Verifies cold-key source ownership through complete application-cache hosts running in independent local operating-system processes.</summary>
public sealed class CacheMultiProcessTests
{
    /// <summary>Exercises actual local host processes at native authority, publication, reader, and persisted-operation boundaries.</summary>
    /// <param name="contract">The fixed contract implemented by the existing production-process probe.</param>
    [SkippableTheory]
    [InlineData("authority-outage")]
    [InlineData("owner-loss")]
    [InlineData("hot-invalidation")]
    [InlineData("tracked-invalidation")]
    [InlineData("tracked-clear")]
    [InlineData("reader-quota")]
    [InlineData("clear-publication")]
    [InlineData("unretained-handoff")]
    [InlineData("source-departure")]
    public async Task ApplicationHosts_AdvancedLocalContract_PreservesAuthoritativeProcessBoundaries(string contract)
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Native filesystem authority supports Windows and Linux.");
        var runId = Guid.NewGuid().ToString("N");
        var workspace = ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-contract-" + contract, true);
        try
        {
            var processes = await CacheSharedProbe.RunLocalContractAsync(workspace, contract);

            processes.Count.ShouldBe(2);
            processes.Distinct().Count().ShouldBe(2);
        }
        finally
        {
            ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-contract-" + contract, false);
            ProbeWorkspace.RejectLinks(workspace);
            Directory.Delete(workspace, true);
        }
    }

    /// <summary>Holds every admitted source until the parent has confirmed the selected process-sharing contract.</summary>
    /// <param name="nodes">The exact number of independent process hosts sharing one local native root.</param>
    /// <param name="scope">Whether each process owns one source or all processes share one authoritative source.</param>
    [SkippableTheory]
    [InlineData(2, CacheCoordinationScope.SharedStore)]
    [InlineData(4, CacheCoordinationScope.SharedStore)]
    [InlineData(2, CacheCoordinationScope.NodeLocal)]
    [InlineData(4, CacheCoordinationScope.NodeLocal)]
    public async Task GetOrLoad_IndependentLocalProcesses_RespectConfiguredSourceOwnership(int nodes, CacheCoordinationScope scope)
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Native filesystem authority supports Windows and Linux.");
        var runId = Guid.NewGuid().ToString("N");
        var workspace = ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-multiprocess", true);
        try
        {
            var callbacks = await CacheSharedProbe.RunLocalAsync(workspace, nodes, scope);

            callbacks.Count.ShouldBe(nodes);
            callbacks.ShouldBe(scope == CacheCoordinationScope.SharedStore
                ? Enumerable.Range(0, nodes).Select(index => index == 0 ? 1 : 0)
                : Enumerable.Repeat(1, nodes));
        }
        finally
        {
            ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-multiprocess", false);
            ProbeWorkspace.RejectLinks(workspace);
            Directory.Delete(workspace, true);
        }
    }
}