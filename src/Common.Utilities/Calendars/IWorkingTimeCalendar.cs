// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Reserves a future abstraction for working-time calendars.
/// </summary>
/// <remarks><example><code>var isWorking = calendar.IsWorkingTime(instant);</code></example></remarks>
public interface IWorkingTimeCalendar
{
    /// <summary>Determines whether an instant falls inside working time.</summary>
    /// <param name="instant">The instant to evaluate.</param>
    /// <returns><c>true</c> when the instant is working time.</returns>
    /// <remarks><example><code>var isWorking = calendar.IsWorkingTime(instant);</code></example></remarks>
    bool IsWorkingTime(DateTimeOffset instant);
}
