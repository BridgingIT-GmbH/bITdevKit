// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using System.Globalization;
using BridgingIT.DevKit.Presentation;
using Spectre.Console;

/// <summary>Verifies per-property opt-in to friendly durations in command binding.</summary>
/// <example><code>var (ok, errors) = ConsoleCommandBinder.TryBind(command, ["--timeout", "500ms"]);</code></example>
public sealed class ConsoleCommandDurationBindingTests
{
    /// <summary>Checks invariant binding for options and nullable positional arguments.</summary>
    /// <example><code>var (ok, errors) = ConsoleCommandBinder.TryBind(command, ["1.5m", "--timeout", "500ms"]);</code></example>
    [Theory]
    [InlineData("en-US")]
    [InlineData("nl-NL")]
    public void TryBind_AnnotatedDurations_UsesFriendlyInvariantGrammar(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var sut = new FriendlyCommand();

            var result = ConsoleCommandBinder.TryBind(sut, ["1.5m", "--timeout", "500ms", "--count", "42"]);

            result.ok.ShouldBeTrue();
            result.errors.ShouldBeEmpty();
            sut.Timeout.ShouldBe(TimeSpan.FromMilliseconds(500));
            sut.Delay.ShouldBe(TimeSpan.FromSeconds(90));
            sut.Count.ShouldBe(42);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Checks signed values in both positional and named duration bindings.</summary>
    /// <example><code>var (ok, errors) = ConsoleCommandBinder.TryBind(command, ["-1.5m", "--timeout", "-500ms"]);</code></example>
    [Theory]
    [InlineData("--timeout")]
    [InlineData("-t")]
    public void TryBind_SignedAnnotatedDurations_BindsNamedAndPositionalValues(string option)
    {
        var sut = new FriendlyCommand();

        var result = ConsoleCommandBinder.TryBind(sut, ["-1.5m", option, "-500ms"]);

        result.ok.ShouldBeTrue();
        result.errors.ShouldBeEmpty();
        sut.Timeout.ShouldBe(TimeSpan.FromMilliseconds(-500));
        sut.Delay.ShouldBe(TimeSpan.FromSeconds(-90));
    }

    /// <summary>Checks that a registered option takes precedence over a signed duration token.</summary>
    /// <example><code>var (ok, errors) = ConsoleCommandBinder.TryBind(command, ["--timeout", "-1m"]);</code></example>
    [Fact]
    public void TryBind_SignedTokenMatchesAlias_BindsRegisteredOption()
    {
        var sut = new FriendlyCommand();

        var result = ConsoleCommandBinder.TryBind(sut, ["--timeout", "-1m"]);

        result.ok.ShouldBeFalse();
        result.errors.ShouldContain("Option --timeout requires a value.");
        sut.Cache.ShouldBeTrue();
        sut.Delay.ShouldBeNull();
    }

    /// <summary>Checks that failed conversion reports the usual diagnostics and preserves values.</summary>
    /// <example><code>var (ok, errors) = ConsoleCommandBinder.TryBind(command, ["bad", "--timeout", "invalid"]);</code></example>
    [Theory]
    [InlineData("bad", "invalid")]
    [InlineData("-1e999h", "-1d")]
    public void TryBind_InvalidDurations_ReturnsBindingErrorsWithoutReplacingValues(string argument, string option)
    {
        var sut = new FriendlyCommand { Timeout = TimeSpan.FromSeconds(2), Delay = TimeSpan.FromSeconds(3) };

        var result = ConsoleCommandBinder.TryBind(sut, [argument, "--timeout", option]);

        result.ok.ShouldBeFalse();
        result.errors.Count.ShouldBe(2);
        result.errors.ShouldContain(error => error.Contains($"Invalid value '{option}' for --timeout", StringComparison.Ordinal));
        result.errors.ShouldContain(error => error.Contains($"Invalid value '{argument}' for argument #0", StringComparison.Ordinal));
        sut.Timeout.ShouldBe(TimeSpan.FromSeconds(2));
        sut.Delay.ShouldBe(TimeSpan.FromSeconds(3));
    }

    /// <summary>Checks that commands without the marker retain standard TimeSpan parsing.</summary>
    /// <example><code>var (ok, errors) = ConsoleCommandBinder.TryBind(command, ["00:01:30", "--timeout", "00:00:01"]);</code></example>
    [Fact]
    public void TryBind_UnannotatedDurations_KeepsStandardTimeSpanGrammar()
    {
        var sut = new StandardCommand();

        ConsoleCommandBinder.TryBind(sut, ["1.5m", "--timeout", "500ms"]).errors.Count.ShouldBe(2);
        ConsoleCommandBinder.TryBind(sut, ["00:01:30", "--timeout", "00:00:01"]).ok.ShouldBeTrue();
        sut.Timeout.ShouldBe(TimeSpan.FromSeconds(1));
        sut.Delay.ShouldBe(TimeSpan.FromSeconds(90));

        var signed = ConsoleCommandBinder.TryBind(sut, ["-1.5m", "--timeout", "-500ms"]);
        signed.errors.ShouldBe(["Option --timeout requires a value."]);
        sut.Timeout.ShouldBe(TimeSpan.FromSeconds(1));
        sut.Delay.ShouldBe(TimeSpan.FromSeconds(90));
    }

    private sealed class FriendlyCommand() : ConsoleCommandBase("friendly", "Bind a test command")
    {
        [ConsoleCommandOption("timeout", Alias = "t"), ConsoleCommandDuration]
        public TimeSpan Timeout { get; set; }

        [ConsoleCommandArgument(0), ConsoleCommandDuration]
        public TimeSpan? Delay { get; set; }

        [ConsoleCommandOption("count"), ConsoleCommandDuration]
        public int Count { get; set; }

        [ConsoleCommandOption("cache", Alias = "1m")]
        public bool Cache { get; set; }

        public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StandardCommand() : ConsoleCommandBase("standard", "Bind standard durations")
    {
        [ConsoleCommandOption("timeout")]
        public TimeSpan Timeout { get; set; }

        [ConsoleCommandArgument(0)]
        public TimeSpan? Delay { get; set; }

        public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
