// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Presentation;

/// <summary>
/// Base class for WeatherFiesta application console subcommands.
/// </summary>
public abstract class AppGroupConsoleCommandBase(string name, string description, params string[] aliases)
    : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <inheritdoc />
    public string GroupName => "app";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => [];
}
