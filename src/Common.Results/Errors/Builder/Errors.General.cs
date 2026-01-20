// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class General
    {
        /// <summary>Creates a generic <see cref="ResultErrorBase"/> error for catch-all scenarios.</summary>
        public static ResultErrorBase Error(string message = null)
            => new GeneralError(message);
    }

    private sealed class GeneralError(string message = null) : ResultErrorBase(message ?? "An error occurred")
    {
    }
}
