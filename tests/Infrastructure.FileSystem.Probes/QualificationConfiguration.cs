namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed record QualificationProfile(string Id, string HostOs, string AccessKind, string ScratchRoot, string ClientVersion, string ServerVersion, string ProtocolVersion, string MountSettings, string ReferenceHostId, string PeerHostId, Dictionary<string, string> Environment);

internal sealed record QualificationConfiguration(int SchemaVersion, QualificationProfile[] Profiles)
{
    internal static QualificationConfiguration LoadConfiguration(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new ConfigurationUnavailableException();
        }

        if (new FileInfo(path).Length > 1_048_576)
        {
            throw new InvalidDataException("Configuration exceeds the one MiB parsing bound.");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 16 });
        RejectCredentials(document.RootElement);

        if (!document.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Require a profiles array.");
        }

        foreach (var profile in profiles.EnumerateArray())
        {
            foreach (var field in new[]
            {
                "id",
                "hostOs",
                "accessKind",
                "scratchRoot",
                "clientVersion",
                "serverVersion",
                "protocolVersion",
                "mountSettings",
                "referenceHostId",
                "peerHostId",
                "environment"
            }
            )
            {
                if (profile.ValueKind != JsonValueKind.Object || !profile.TryGetProperty(field, out _))
                {
                    throw new InvalidDataException("Missing qualification profile field; values withheld.");
                }
            }
        }

        var configuration = document.Deserialize<QualificationConfiguration>(ProbeResult.JsonOptions);
        ValidateProfiles(configuration);

        return configuration;
    }

    internal static void ValidateProfiles(QualificationConfiguration configuration)
    {
        if (configuration is not { SchemaVersion: 1, Profiles.Length: > 0 })
        {
            throw new InvalidDataException("Require schemaVersion 1 and nonempty profiles.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profile in configuration.Profiles)
        {
            if (profile is null || !SafeId(profile.Id) || !ids.Add(profile.Id)
                || profile.HostOs is not ("Windows" or "Linux")
                || profile.AccessKind is not ("Local" or "Smb" or "Nfs")
                || (profile.HostOs == "Windows" && profile.AccessKind == "Nfs")
                || !AbsoluteRoot(profile.HostOs, profile.ScratchRoot) || new[]
            {
                profile.ClientVersion,
                profile.ServerVersion,
                profile.ProtocolVersion,
                profile.MountSettings,
                profile.ReferenceHostId
            }.Any(string.IsNullOrWhiteSpace) || profile.Environment is not { Count: > 0 } || profile.Environment.Any(p => string.IsNullOrWhiteSpace(p.Value)) || (profile.AccessKind == "Local" ? profile.PeerHostId is not null
                    : string.IsNullOrWhiteSpace(profile.PeerHostId) || profile.ReferenceHostId == profile.PeerHostId))
            {
                throw new InvalidDataException("Invalid qualification profile; values withheld.");
            }
        }
    }

    internal static bool SafeId(string value)
    {
        return value is not null && Regex.IsMatch(value, "^[a-zA-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant);
    }

    private static bool AbsoluteRoot(string os, string path)
    {
        return !string.IsNullOrWhiteSpace(path)
        && (os == "Linux" ? path.StartsWith('/') : Regex.IsMatch(path, @"^(?:[a-zA-Z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)", RegexOptions.CultureInvariant))
        && !path.Split(['/', '\\']).Any(p => p is "." or "..");
    }

    private static void RejectCredentials(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (Regex.IsMatch(property.Name, "password|passwd|secret|token|credential|authorization|private.?key", RegexOptions.IgnoreCase))
                {
                    throw new InvalidDataException("Credential-bearing configuration is prohibited; values withheld.");
                }

                RejectCredentials(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectCredentials(item);
            }
        }
        else if (element.ValueKind == JsonValueKind.String && Regex.IsMatch(element.GetString() ?? "",
            @"(?i)(password|passwd|pwd|secret|token|credential|authorization|username|user|accountkey|sig)\s*[=:]|://[^/\s]*@|-----BEGIN .*PRIVATE KEY-----"))
        {
            throw new InvalidDataException("Credential-bearing configuration is prohibited; values withheld.");
        }
    }
}

internal sealed class ConfigurationUnavailableException : Exception;