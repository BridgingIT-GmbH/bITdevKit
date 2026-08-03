// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Computes and validates canonical provider-neutral SHA-256 content hashes.
/// </summary>
/// <example>
/// <code>
/// var hash = ContentHashHelper.ComputeSha256("payload"u8);
/// </code>
/// </example>
public static partial class ContentHashHelper
{
    /// <summary>Gets the canonical SHA-256 prefix.</summary>
    public const string Sha256Prefix = "sha256:";

    /// <summary>Computes a canonical SHA-256 hash for bytes.</summary>
    /// <param name="content">The content bytes.</param>
    /// <returns>The canonical content hash.</returns>
    /// <example><code>var hash = ContentHashHelper.ComputeSha256(bytes);</code></example>
    public static string ComputeSha256(ReadOnlySpan<byte> content) =>
        FormatSha256(Convert.ToHexStringLower(SHA256.HashData(content)));

    /// <summary>Computes a canonical SHA-256 hash for UTF-8 text.</summary>
    /// <param name="content">The text.</param>
    /// <returns>The canonical content hash.</returns>
    /// <example><code>var hash = ContentHashHelper.ComputeSha256("key");</code></example>
    public static string ComputeSha256(string content) =>
        content is null ? null : ComputeSha256(Encoding.UTF8.GetBytes(content));

    /// <summary>Computes a canonical SHA-256 hash while copying a stream to a destination.</summary>
    /// <param name="source">The readable source.</param>
    /// <param name="destination">The writable destination.</param>
    /// <param name="maximumBytes">The optional byte limit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The copy result with a canonical hash.</returns>
    /// <example><code>var result = await ContentHashHelper.CopyAndComputeSha256Async(source, target);</code></example>
    public static async Task<ContentHashCopyResult> CopyAndComputeSha256Async(
        Stream source,
        Stream destination,
        long? maximumBytes = null,
        CancellationToken cancellationToken = default)
    {
        var result = await StreamHelper.CopyAsync(
                source,
                destination,
                new StreamCopyOptions
                {
                    HashAlgorithm = HashAlgorithmName.SHA256,
                    MaximumBytes = maximumBytes
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new ContentHashCopyResult(result.Length, FormatSha256(result.Hash));
    }

    /// <summary>Formats a lowercase SHA-256 hexadecimal value.</summary>
    /// <param name="lowercaseHex">The 64-character lowercase value.</param>
    /// <returns>The canonical content hash.</returns>
    /// <example><code>var hash = ContentHashHelper.FormatSha256(hex);</code></example>
    public static string FormatSha256(string lowercaseHex)
    {
        if (string.IsNullOrWhiteSpace(lowercaseHex) || !LowercaseSha256Regex().IsMatch(lowercaseHex))
        {
            throw new FormatException("SHA-256 value must contain exactly 64 lowercase hexadecimal characters.");
        }

        return Sha256Prefix + lowercaseHex;
    }

    /// <summary>Determines whether a value is a canonical SHA-256 content hash.</summary>
    /// <param name="value">The value.</param>
    /// <returns>True when valid.</returns>
    /// <example><code>var valid = ContentHashHelper.IsSha256(hash);</code></example>
    public static bool IsSha256(string value) =>
        !string.IsNullOrWhiteSpace(value) && CanonicalSha256Regex().IsMatch(value);

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex LowercaseSha256Regex();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSha256Regex();
}

/// <summary>Describes a copied byte count and canonical content hash.</summary>
/// <param name="Length">The copied byte count.</param>
/// <param name="ContentHash">The canonical content hash.</param>
/// <example><code>var hash = result.ContentHash;</code></example>
public sealed record ContentHashCopyResult(long Length, string ContentHash);
