// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a data integrity or consistency violation error.
/// </summary>
public class DataIntegrityError(string message = null) : ResultErrorBase(message ?? "Data integrity violation")
{
    public DataIntegrityError() : this(null)
    {
    }
}