// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents a conflict between a requested operation and existing state.</summary>
/// <param name="message">The conflict description, or <see langword="null"/> to use <c>Conflict</c>.</param>
public class ConflictError(string message = null) : ResultErrorBase(message ?? "Conflict")
{
    /// <summary>Initializes a conflict error with the default message.</summary>
    public ConflictError() : this(null)
    {
    }
}
