// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error indicating that data has changed since it was loaded.
/// </summary>
public class StaleDataError(string message = null) : ResultErrorBase(message ?? "Data has changed since it was loaded")
{
}