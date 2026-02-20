// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class DatabaseCheckerOptions : OptionsBase
{
    /// <summary>
    ///     Gets or sets a value indicating whether the application process should be terminated immediately when database checking fails.
    /// </summary>
    /// <remarks>
    ///     When enabled, a failure triggers <see cref="Environment.FailFast(string, Exception)" /> after logging.
    /// </remarks>
    public bool HaltOnFailure { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the database checker is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the delay before the database checker starts.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;
}
