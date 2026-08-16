// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents a failure to apply or persist a requested change.</summary>
/// <param name="message">The failure description, or <see langword="null"/> to use <c>Change</c>.</param>
public class ChangeError(string message = null) : ResultErrorBase(message ?? "Change")
{
    /// <summary>Initializes a change error with the default message.</summary>
    public ChangeError() : this(null)
    {
    }
}
