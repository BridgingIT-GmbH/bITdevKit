// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Collections.Generic;

/// <summary>
/// Defines a console command that is part of a command group.
/// </summary>
public interface IGroupedConsoleCommand : IConsoleCommand
{
    /// <summary>
    /// Gets the group name this command belongs to.
    /// </summary>
    string GroupName { get; }

    /// <summary>
    /// Gets optional group name aliases.
    /// </summary>
    IReadOnlyCollection<string> GroupAliases { get; }
}