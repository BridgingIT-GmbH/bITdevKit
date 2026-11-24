namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

public class CacheCommitRecoveryPrototypeTests
{
    [SkippableFact]
    public async Task CommitBoundaries_ProcessTermination_PreservesCheckedSnapshotsFencesAndUncertainCredit()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Native durability supports Windows and Linux.");
        var runId = Guid.NewGuid().ToString("N");
        var workspace = Path.Combine(Path.GetTempPath(), "bdk-filesystem-probes", runId, "cache-prototype");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "run.json"), $"bdk-filesystem-probes/1/{runId}/cache-prototype");

        var results = await CacheCommitRecoveryPrototype.RunAsync(workspace);

        results.Count.ShouldBe(9);
        results.ShouldContain("manifest-published");
        results.ShouldContain("invalidation-fence");
        results.ShouldContain("absent-key-fence");
        results.ShouldContain("checked-manifest-corruption");
    }
}