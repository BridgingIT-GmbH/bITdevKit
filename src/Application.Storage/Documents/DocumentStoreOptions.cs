// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures document-store query safety limits.
/// </summary>
public sealed class DocumentStoreOptions
{
    /// <summary>
    /// Gets or sets the default page size when a query does not specify <see cref="DocumentQuery.Take" />.
    /// </summary>
    public int DefaultTake { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum allowed page size.
    /// </summary>
    public int MaxTake { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether type-wide scans are globally allowed.
    /// </summary>
    public bool AllowFullScans { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether client-side filtered queries should fail.
    /// </summary>
    public bool RejectClientSideFilteredQueries { get; set; } = true;

    /// <summary>
    /// Validates option values.
    /// </summary>
    public Result Validate()
    {
        if (this.DefaultTake <= 0)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("DefaultTake must be greater than zero."));
        }

        if (this.MaxTake <= 0)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("MaxTake must be greater than zero."));
        }

        if (this.DefaultTake > this.MaxTake)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("DefaultTake must be less than or equal to MaxTake."));
        }

        return Result.Success();
    }
}
