namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Text.Json;

internal sealed record ProbeCase(string Name, string Status, string Detail);

internal sealed record ProbeResult(string TaskId, string RunId, string ProfileId, IReadOnlyList<ProbeCase> Cases, string[] ArtifactPaths)
{
    public int SchemaVersion => 1;

    public string Status => this.Cases.Any(c => c.Status == "failed") ? "failed" : this.Cases.Any(c => c.Status == "unavailable") ? "unavailable" : "passed";

    public int CaseCount => this.Cases.Count;

    public int ExitCode => this.Status == "passed" ? 0 : this.Status == "failed" ? 1 : 2;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    internal static void Write(string path, object value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }
}