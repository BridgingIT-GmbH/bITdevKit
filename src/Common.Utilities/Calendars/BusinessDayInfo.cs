// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes why a date is or is not a business day.
/// </summary>
/// <param name="Date">The evaluated date.</param>
/// <param name="IsBusinessDay">Whether the date is a business day.</param>
/// <param name="Reason">Optional diagnostic reason.</param>
/// <remarks><example><code>var info = calendar.GetBusinessDayInfo(date);</code></example></remarks>
public sealed record BusinessDayInfo(DateOnly Date, bool IsBusinessDay, string Reason = null);
