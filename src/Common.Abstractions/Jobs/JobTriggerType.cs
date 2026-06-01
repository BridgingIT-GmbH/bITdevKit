// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the configured trigger type for a job definition.
/// </summary>
public enum JobTriggerType
{
    /// <summary>
    /// Runs only when an explicit manual dispatch is requested.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Runs once at a fixed UTC instant.
    /// </summary>
    OneTime = 1,

    /// <summary>
    /// Runs once after a delay from activation.
    /// </summary>
    Delayed = 2,

    /// <summary>
    /// Runs once after a delay from scheduler startup.
    /// </summary>
    StartupDelay = 3,

    /// <summary>
    /// Runs on a cron-based schedule.
    /// </summary>
    Cron = 4,

    /// <summary>
    /// Runs on a calendar-based schedule.
    /// </summary>
    Calendar = 5,

    /// <summary>
    /// Runs in response to an application event.
    /// </summary>
    Event = 6,

    /// <summary>
    /// Runs according to custom provider-defined evaluation logic.
    /// </summary>
    Custom = 7,
}