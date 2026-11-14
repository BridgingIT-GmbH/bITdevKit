// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

/// <summary>Exercises the independent controller/peer transport on local TEMP without claiming remote share qualification.</summary>
public sealed class CacheSharedRoleTests
{
    /// <summary>Local transport cannot claim a physical share disconnect even when requested explicitly.</summary>
    [Fact]
    public async Task DisconnectReconnect_LocalTransport_ReportsUnavailable()
    {
        var runId = Guid.NewGuid().ToString("N");
        var evidence = ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-role-unavailable", true);
        try
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["--local"] = "true", ["--role"] = "controller", ["--contract"] = "disconnect-reconnect"
            };
            var result = await CacheSharedContractProbe.RunAsync(evidence, runId, arguments);

            result.ShouldBe(2);
            File.Exists(Path.Combine(evidence, "cache-shared.json")).ShouldBeTrue();
        }
        finally
        {
            ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-role-unavailable", false);
            ProbeWorkspace.RejectLinks(evidence);
            Directory.Delete(evidence, true);
        }
    }

    /// <summary>Runs the same advanced actor coordinator used by qualified shared-host profiles.</summary>
    /// <param name="contract">The bounded actor contract.</param>
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
    public Task ControllerAndPeer_AdvancedContract_VerifyActualActors(string contract) => RunAsync(contract, 2, "SharedStore");

    /// <summary>Verifies two/four actual child hosts through the independently supervised source-scope protocol.</summary>
    /// <param name="nodes">The number of independent actor processes.</param>
    /// <param name="scope">The declared per-node or shared-store source scope.</param>
    [SkippableTheory]
    [InlineData(2, "NodeLocal")]
    [InlineData(2, "SharedStore")]
    [InlineData(4, "NodeLocal")]
    [InlineData(4, "SharedStore")]
    public Task ControllerAndPeer_ColdLoad_VerifyConfiguredProcessScope(int nodes, string scope) => RunAsync("cold-load", nodes, scope);

    private static async Task RunAsync(string contract, int nodes, string scope)
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Native filesystem authority supports Windows and Linux.");
        var runId = Guid.NewGuid().ToString("N");
        var profile = "cache-roles-" + contract;
        var evidenceProfile = "cache-roles-evidence";
        var evidence = ProbeWorkspace.Open(Path.GetTempPath(), runId, evidenceProfile, true);
        try
        {
            var controller = Options("controller");
            var peer = Options("peer");
            var results = await Task.WhenAll(CacheSharedContractProbe.RunAsync(evidence, runId, controller), CacheSharedContractProbe.RunAsync(evidence, runId, peer));

            results.ShouldBe([0, 0]);
            File.Exists(Path.Combine(evidence, "cache-shared-controller.json")).ShouldBeTrue();
            File.Exists(Path.Combine(evidence, "cache-shared-peer.json")).ShouldBeTrue();
        }
        finally
        {
            var workspace = ProbeWorkspace.Open(Path.GetTempPath(), runId, profile, false);
            ProbeWorkspace.RejectLinks(workspace);
            Directory.Delete(workspace, true);
            ProbeWorkspace.Open(Path.GetTempPath(), runId, evidenceProfile, false);
            ProbeWorkspace.RejectLinks(evidence);
            Directory.Delete(evidence, true);
        }

        Dictionary<string, string> Options(string role) => new(StringComparer.Ordinal)
        {
            ["--local"] = "true", ["--role"] = role, ["--contract"] = contract, ["--run-id"] = runId,
            ["--nodes"] = nodes.ToString(System.Globalization.CultureInfo.InvariantCulture), ["--scope"] = scope
        };
    }
}