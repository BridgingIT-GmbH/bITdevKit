// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class ProcessingOptions
{
    public bool ContinueOnItemFailure { get; set; } = true;

    public int? MaxFailures { get; set; }

    public bool IncludeFailedItems { get; set; }

    public static ProcessingOptions Default => new()
    {
        ContinueOnItemFailure = true,
        MaxFailures = null,
        IncludeFailedItems = false
    };

    public static ProcessingOptions Strict => new()
    {
        ContinueOnItemFailure = false,
        MaxFailures = 0,
        IncludeFailedItems = false
    };
}