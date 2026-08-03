// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.RegularExpressions;

/// <summary>
/// Provides blob content SHA-256 hash helpers.
/// </summary>
/// <example>
/// <code>
/// var result = await BlobContentHash.ComputeSha256Async(upload.Content, cancellationToken);
/// </code>
/// </example>
public static partial class BlobContentHash
{
    /// <summary>
    /// Gets the provider-neutral SHA-256 blob hash prefix.
    /// </summary>
    /// <example>
    /// <code>
    /// var prefix = BlobContentHash.Prefix;
    /// </code>
    /// </example>
    public const string Prefix = "sha256:";

    /// <summary>
    /// Computes a provider-neutral SHA-256 blob content hash.
    /// </summary>
    /// <param name="content">The readable content stream to hash from its current position.</param>
    /// <param name="cancellationToken">The cancellation token used while reading the stream.</param>
    /// <returns>A result containing the formatted SHA-256 blob content hash.</returns>
    /// <example>
    /// <code>
    /// var hash = await BlobContentHash.ComputeSha256Async(content, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result<string>> ComputeSha256Async(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (content is null)
        {
            return Result<string>.Failure(new BlobStoreValidationError("Blob content stream is required."));
        }

        if (!content.CanRead)
        {
            return Result<string>.Failure(new BlobStoreValidationError("Blob content stream must be readable."));
        }

        var hash = await HashHelper.ComputeSha256Async(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Result<string>.Success(FormatSha256(hash));
    }

    /// <summary>
    /// Validates an optional expected blob content hash.
    /// </summary>
    /// <param name="expectedContentHash">The expected content hash value.</param>
    /// <returns>A success result when the value is null, empty, or in the required format.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobContentHash.ValidateExpectedHash(upload.ExpectedContentHash);
    /// </code>
    /// </example>
    public static Result ValidateExpectedHash(string expectedContentHash)
    {
        if (string.IsNullOrWhiteSpace(expectedContentHash))
        {
            return Result.Success();
        }

        return IsSha256Hash(expectedContentHash)
            ? Result.Success()
            : Result.Failure(new BlobStoreValidationError("ExpectedContentHash must use the format 'sha256:&lt;lowercase-64-character-hex&gt;'."));
    }

    /// <summary>
    /// Determines whether a value uses the provider-neutral SHA-256 blob hash format.
    /// </summary>
    /// <param name="contentHash">The content hash value to inspect.</param>
    /// <returns><c>true</c> when the value matches <c>sha256:&lt;lowercase-64-character-hex&gt;</c>.</returns>
    /// <example>
    /// <code>
    /// var valid = BlobContentHash.IsSha256Hash(hash);
    /// </code>
    /// </example>
    public static bool IsSha256Hash(string contentHash) =>
        !string.IsNullOrWhiteSpace(contentHash) && Sha256HashRegex().IsMatch(contentHash);

    private static string FormatSha256(string lowercaseHex) => $"{Prefix}{lowercaseHex}";

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256HashRegex();
}
