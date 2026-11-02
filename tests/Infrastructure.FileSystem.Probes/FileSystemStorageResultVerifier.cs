namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Text.Json;

internal static class FileSystemStorageResultVerifier
{
    internal static int Verify(string evidence, string runId)
    {
        var path = Path.Combine(evidence, "load.json");

        if (!File.Exists(path))
        {
            ProbeResult.Write(Path.Combine(evidence, "verification.json"), new ProbeResult("TASK-022-verify", runId, "local", [new("load-result", "unavailable", "Explicit run has no load.json.")], []));

            return 2;
        }

        var cases = new List<ProbeCase>();
        VerifyFile(path, cases, runId, Path.GetFullPath(Path.GetDirectoryName(evidence)), []);
        var result = new ProbeResult("TASK-022-verify", runId, "local", cases, ["load.json"]);
        ProbeResult.Write(Path.Combine(evidence, "verification.json"), result);
        Console.WriteLine($"Local result verification {result.Status}; {evidence}");

        return result.ExitCode;
    }

    private static void VerifyFile(string path, List<ProbeCase> cases, string runId, string allowedRoot, HashSet<string> visited)
    {
        if (!visited.Add(Path.GetFullPath(path)) || visited.Count > 32)
        {
            throw new InvalidDataException("Cyclic or oversized result index.");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;

        if (root.GetProperty("runId").GetString() != runId)
        {
            throw new InvalidDataException("Selected child run identity differs.");
        }

        if (root.TryGetProperty("processArtifacts", out var children))
        {
            cases.Add(new("controller-outcome", root.GetProperty("status").GetString() == "passed" ? "passed" : "failed", "Controller and child completion state."));

            foreach (var child in children.EnumerateArray())
            {
                var relative = child.GetString();

                if (relative is null || Path.IsPathFullyQualified(relative))
                {
                    throw new InvalidDataException("Invalid child artifact path.");
                }

                var childPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), relative));

                if (!childPath.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.GetFileName(childPath) != "load.json")
                {
                    throw new InvalidDataException("Child artifact escapes the evidence root.");
                }

                VerifyFile(childPath, cases, runId, allowedRoot, visited);
            }

            return;
        }

        var valid = root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("status").GetString() == "passed";

        if (root.TryGetProperty("postLoadVerification", out var verification))
        {
            var artifact = verification.GetString();

            if (Path.GetFileName(artifact) != artifact)
            {
                throw new InvalidDataException("Invalid verification artifact.");
            }

            using var verified = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), artifact)));
            valid &= verified.RootElement.GetProperty("status").GetString() == "passed" && verified.RootElement.GetProperty("verifiedHashes").GetInt64() > 0;
        }

        foreach (var indexed in root.GetProperty("results").EnumerateArray())
        {
            var artifact = indexed.GetProperty("artifact").GetString();

            if (artifact is null || Path.GetFileName(artifact) != artifact || !artifact.StartsWith("load-repetition-", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Invalid repetition artifact.");
            }

            using var repetitionFile = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), artifact)));
            var repetition = repetitionFile.RootElement;
            long successes = 0;

            foreach (var item in repetition.GetProperty("cases").EnumerateArray())
            {
                var count = item.GetProperty("successes").GetInt64();
                var errors = item.GetProperty("errors").EnumerateObject().Sum(p => p.Value.GetInt64());
                var sampleCount = item.GetProperty("rawLatenciesMicroseconds").GetArrayLength();
                var conflicts = item.GetProperty("expectedConflicts").GetInt64();
                var passed = count > 0 && errors == 0 && count == sampleCount && count + conflicts == item.GetProperty("attempts").GetInt64()
                    && (conflicts == 0 || item.GetProperty("name").GetString() == "hot-key-etag")
                    && (count >= 10000 || item.GetProperty("p99").ValueKind == JsonValueKind.Null);
                valid &= passed;
                successes += count;
                cases.Add(new(item.GetProperty("name").GetString(), passed ? "passed" : "failed", $"successes={count}; expectedConflicts={conflicts}; errors={errors}; samples={sampleCount}"));
            }

            valid &= successes == repetition.GetProperty("successfulOperations").GetInt64();
        }

        cases.Add(new("run-accounting", valid ? "passed" : "failed", "Verified explicit run case counts, errors, raw samples and p99 suppression. No support or performance-budget acceptance is inferred."));
    }
}