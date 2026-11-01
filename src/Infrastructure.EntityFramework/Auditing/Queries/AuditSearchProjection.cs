// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Buffers;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Builds and streams version-two search projections while retaining version-one verification support.</summary>
/// <remarks>All supported typed detail conversions are recorded independently of active query registrations. Source property values are never indexed.</remarks>
/// <example><code>var searchJson = AuditSearchProjection.Build(record, "Record");</code></example>
public static class AuditSearchProjection
{
    private static readonly AuditFieldCatalog catalog = new();
    private static readonly string[] detailKinds = ["Boolean", "Decimal", "Guid", "Integer", "String", "Timestamp"];

    /// <summary>Creates normalized storage JSON using explicit canonical property and child-array order.</summary>
    /// <example><code>var search = AuditSearchProjection.Build(action, "Action");</code></example>
    public static string Build(object instance, string domain) => BuildCore(instance, domain, 2);

    private static string BuildCore(object instance, string domain, int version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, instance, domain, version);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Computes the expected projection digest without retaining its expanded JSON text.</summary>
    /// <example><code>var expected = AuditSearchProjection.ComputeHash(original, "Record");</code></example>
    public static string ComputeHash(object instance, string domain) => ComputeHash(instance, domain, 2);

    internal static string ComputeLegacyHash(object instance, string domain) => ComputeHash(instance, domain, 1);

    private static string ComputeHash(object instance, string domain, int version)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var stream = new HashStream(hash))
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, instance, domain, version);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Hashes actual database projection text through bounded buffers, rejecting oversized or non-ASCII text.</summary>
    /// <remarks>The result is computed from the read stream, never taken from an editable stored plain checksum.</remarks>
    /// <example><code>var actual = await AuditSearchProjection.HashStoredAsync(reader, maximumBytes, cancellationToken);</code></example>
    public static async Task<string> HashStoredAsync(TextReader reader, long maximumBytes, CancellationToken cancellationToken = default)
    {
        var characters = ArrayPool<char>.Shared.Rent(2_048);
        var bytes = ArrayPool<byte>.Shared.Rent(2_048);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(characters.AsMemory(0, 2_048), cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                total += count;
                if (total > maximumBytes) throw new ArgumentException("AuditSearchProjectionTooLarge");
                for (var index = 0; index < count; index++)
                {
                    // Supported profiles contain only ASCII keys, markers, numeric keys and encoded UTF-16.
                    if (characters[index] > 127) throw new ArgumentException("AuditSearchProjectionInvalid");
                    bytes[index] = (byte)characters[index];
                }

                hash.AppendData(bytes.AsSpan(0, count));
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<char>.Shared.Return(characters, clearArray: true);
            ArrayPool<byte>.Shared.Return(bytes, clearArray: true);
        }
    }

    /// <summary>Checks a retained in-memory projection against source facts while allowing missing additive members.</summary>
    /// <example><code>var valid = AuditSearchProjection.IsConsistent(searchJson, original, "Record");</code></example>
    public static bool IsConsistent(string json, object original, string domain)
    {
        using var stored = JsonDocument.Parse(json);
        if (stored.RootElement.ValueKind != JsonValueKind.Object || !stored.RootElement.TryGetProperty("version", out var profile) || !profile.TryGetInt32(out var version)) return false;
        if (version is not (1 or 2)) return false;
        using var rebuilt = JsonDocument.Parse(BuildCore(original, domain, version));
        return Subset(stored.RootElement, rebuilt.RootElement);
    }

    private static void Write(Utf8JsonWriter writer, object instance, string domain, int version)
    {
        writer.WriteStartObject();
        // Upper-case collection names precede lower-case fields/version under ordinal canonical ordering.
        if (instance is AuditRecord root)
        {
            Array(writer, "Errors", root.Errors, "Error", version);
            Array(writer, "Resources", root.Resources, "Resource", version);
        }
        else if (instance is AuditAction action)
        {
            Array(writer, "Errors", action.Errors, "Error", version);
            Array(writer, "Resources", action.Resources, "Resource", version);
        }
        else if (instance is AuditChangeEvidence change) Array(writer, "Properties", change.Properties, "Property", version);

        var fields = new SortedDictionary<string, SortedDictionary<string, AuditQueryValue>>(StringComparer.Ordinal);
        foreach (var field in catalog.Fields.Where(f => f.Domain == domain && f.Kind != "Collection" && f.Binding != "StorageState").DistinctBy(f => f.Binding))
            fields[AuditSearchEncoding.FieldKey(field.Binding)] = new(StringComparer.Ordinal)
            {
                [field.Kind] = version == 1 && field.Binding == "HasTruncation" && instance is AuditRecord historical
                    ? new() { Kind = "Boolean", Boolean = historical.OmittedActionCount > 0 || historical.OmittedDetailCount > 0 }
                    : AuditFieldReader.Read(field, instance)
            };
        var details = instance switch { AuditRecord record => record.Details, AuditAction current => current.Details, _ => ImmutableDictionary<string, AuditValue>.Empty };
        foreach (var detail in details)
        {
            var typed = new SortedDictionary<string, AuditQueryValue>(StringComparer.Ordinal);
            foreach (var kind in detailKinds)
            {
                var field = new AuditQueryField { Domain = domain, Binding = "Details." + detail.Key, Kind = kind, Detail = new() { Name = detail.Key, Kind = kind } };
                typed[kind] = AuditFieldReader.Read(field, instance);
            }

            fields[AuditSearchEncoding.FieldKey("Details." + detail.Key)] = typed;
            var marker = new AuditQueryField { Domain = domain, Binding = "Details." + detail.Key + ".Marker", Kind = "String", IsMarker = true, Detail = new() { Name = detail.Key } };
            fields[AuditSearchEncoding.FieldKey(marker.Binding)] = new(StringComparer.Ordinal) { ["String"] = AuditFieldReader.Read(marker, instance) };
        }

        writer.WriteStartObject("fields");
        foreach (var field in fields)
        {
            writer.WriteStartObject(field.Key);
            foreach (var typed in field.Value)
            {
                writer.WriteStartObject(typed.Key);
                writer.WriteString("exact", AuditSearchEncoding.Scalar(typed.Value, false));
                writer.WriteString("folded", AuditSearchEncoding.Scalar(typed.Value, true));
                writer.WriteString("kind", typed.Value.Kind);
                writer.WriteEndObject();
                writer.Flush();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteNumber("version", version);
        writer.WriteEndObject();
        writer.Flush();
    }
    private static void Array<T>(Utf8JsonWriter writer, string name, IEnumerable<T> instances, string domain, int version)
    {
        writer.WriteStartArray(name);
        foreach (var instance in instances) Write(writer, instance, domain, version);
        writer.WriteEndArray();
    }
    private static bool Subset(JsonElement stored, JsonElement expected)
    {
        if (stored.ValueKind != expected.ValueKind) return false;
        if (stored.ValueKind == JsonValueKind.Object) return stored.EnumerateObject().All(property => expected.TryGetProperty(property.Name, out var value) && Subset(property.Value, value));
        if (stored.ValueKind == JsonValueKind.Array) return stored.GetArrayLength() == expected.GetArrayLength() && stored.EnumerateArray().Zip(expected.EnumerateArray()).All(pair => Subset(pair.First, pair.Second));
        return stored.GetRawText() == expected.GetRawText();
    }
    private sealed class HashStream(IncrementalHash hash) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count) => hash.AppendData(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => hash.AppendData(buffer);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
