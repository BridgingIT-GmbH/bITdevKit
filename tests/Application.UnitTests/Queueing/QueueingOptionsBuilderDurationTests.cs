// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using System.Globalization;
using BridgingIT.DevKit.Application.Queueing;

/// <summary>Verifies the opt-in invariant startup delay and existing string overload.</summary>
/// <example><code>var options = new QueueingOptionsBuilder().StartupDelayInvariant("500ms").Build();</code></example>
public sealed class QueueingOptionsBuilderDurationTests
{
    /// <summary>Checks explicit duration parsing, state preservation on invalid text, and the legacy overload.</summary>
    /// <example><code>builder.StartupDelayInvariant("1.5m");</code></example>
    [Theory]
    [InlineData("en-US")]
    [InlineData("nl-NL")]
    public void StartupDelayInvariant_WithFriendlyDurations_UsesTheTypedSetter(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var sut = new QueueingOptionsBuilder().StartupDelay(1000);

            sut.StartupDelay("500ms").Build().StartupDelay.ShouldBe(TimeSpan.FromSeconds(1));
            sut.StartupDelayInvariant("500ms").Build().StartupDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
            sut.StartupDelayInvariant("1.5m").Build().StartupDelay.ShouldBe(
                new QueueingOptionsBuilder().StartupDelay(TimeSpan.FromSeconds(90)).Build().StartupDelay);

            foreach (var invalid in new[] { "invalid", null, "1e100h" })
            {
                Should.Throw<ArgumentException>(() => sut.StartupDelayInvariant(invalid)).ParamName.ShouldBe("value");
                sut.Build().StartupDelay.ShouldBe(TimeSpan.FromSeconds(90));
            }

            sut.StartupDelay("00:00:30").Build().StartupDelay.ShouldBe(TimeSpan.FromSeconds(30));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
