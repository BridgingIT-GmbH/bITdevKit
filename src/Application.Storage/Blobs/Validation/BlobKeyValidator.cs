// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Validates provider-neutral blob key requirements.
/// </summary>
/// <example>
/// <code>
/// var validation = BlobKeyValidator.Validate(new BlobKey("reports", "2026/06/report.pdf"));
/// </code>
/// </example>
public static class BlobKeyValidator
{
    /// <summary>
    /// Validates that a blob key has the required provider-neutral fields.
    /// </summary>
    /// <param name="key">The blob key to validate.</param>
    /// <returns>A success result when the key is valid, or a validation error result.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobKeyValidator.Validate(new BlobKey("exports", "customer/42/export.csv"));
    /// </code>
    /// </example>
    public static Result Validate(BlobKey key) => BlobValidator.Validate(key);
}
