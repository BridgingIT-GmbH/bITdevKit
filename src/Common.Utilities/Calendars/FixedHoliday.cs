// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a fixed annual holiday.
/// </summary>
/// <param name="Month">The month.</param>
/// <param name="Day">The day.</param>
/// <param name="Name">The optional holiday name.</param>
/// <remarks><example><code>var holiday = new FixedHoliday(1, 1, "New Year");</code></example></remarks>
public readonly record struct FixedHoliday(int Month, int Day, string Name = null);
