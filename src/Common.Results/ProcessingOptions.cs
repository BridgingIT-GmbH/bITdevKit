// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Controls how sequence-processing operations respond to individual item failures.</summary>
public class ProcessingOptions
{
    /// <summary>Gets or sets whether processing continues after an item fails.</summary>
    public bool ContinueOnItemFailure { get; set; } = true;

    /// <summary>Gets or sets the maximum number of item failures tolerated, or <see langword="null"/> for no configured limit.</summary>
    public int? MaxFailures { get; set; }

    /// <summary>Gets or sets whether failed items are included in the processing output.</summary>
    public bool IncludeFailedItems { get; set; }

    /// <summary>Gets options that continue after failures, impose no failure limit, and omit failed items.</summary>
    public static ProcessingOptions Default => new()
    {
        ContinueOnItemFailure = true,
        MaxFailures = null,
        IncludeFailedItems = false
    };

    /// <summary>Gets options that stop on the first failure and omit failed items.</summary>
    public static ProcessingOptions Strict => new()
    {
        ContinueOnItemFailure = false,
        MaxFailures = 0,
        IncludeFailedItems = false
    };
}
