// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Defines the Spectre.Console styles used by the native DevKit console and console log sink.
/// </summary>
/// <param name="Name">The stable theme name used by commands and persisted preferences.</param>
/// <param name="DisplayName">The display name shown to users.</param>
/// <param name="PromptStyle">The style used for the interactive command prompt.</param>
/// <param name="AccentStyle">The style used for accent output.</param>
/// <param name="MutedStyle">The style used for muted output.</param>
/// <param name="VerboseStyle">The log style for verbose events.</param>
/// <param name="DebugStyle">The log style for debug events.</param>
/// <param name="InformationStyle">The log style for information events.</param>
/// <param name="WarningStyle">The log style for warning events.</param>
/// <param name="ErrorStyle">The log style for error events.</param>
/// <param name="FatalStyle">The log style for fatal events.</param>
/// <example>
/// <code>
/// var style = ConsoleTheme.Current.InformationStyle;
/// </code>
/// </example>
public sealed record ConsoleThemePalette(
    string Name,
    string DisplayName,
    string PromptStyle,
    string AccentStyle,
    string MutedStyle,
    string VerboseStyle,
    string DebugStyle,
    string InformationStyle,
    string WarningStyle,
    string ErrorStyle,
    string FatalStyle);
