namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

internal static class ProbeWorkspace
{
    internal static string Open(string scratchRoot, string runId, string profileId, bool create)
    {
        if (!Guid.TryParseExact(runId, "N", out _) || !QualificationConfiguration.SafeId(profileId) || !Path.IsPathFullyQualified(scratchRoot))
        {
            throw new InvalidDataException("Invalid probe workspace identity.");
        }

        var root = Path.GetFullPath(scratchRoot);
        RejectLinks(root);
        var path = Path.Combine(root, "bdk-filesystem-probes", runId, profileId);
        RejectLinks(path);
        var marker = Path.Combine(path, "run.json");
        var identity = $"bdk-filesystem-probes/1/{runId}/{profileId}";

        if (create)
        {
            Directory.CreateDirectory(path);
            using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(identity);
        }
        else if (!File.Exists(marker) || File.ReadAllText(marker) != identity)
        {
            throw new InvalidDataException("Peer must join an existing marked workspace.");
        }

        return path;
    }

    internal static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Probe paths cannot traverse links or reparse points.");
            }
        }
    }
}