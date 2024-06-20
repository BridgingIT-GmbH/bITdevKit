// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using BridgingIT.DevKit.Common;

public class DatabaseMigratorOptions : OptionsBase
{
    public bool Enabled { get; set; } = true;

    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    public bool EnsureDeleted { get; set; }

    public bool EnsureTruncated { get; set; }

    public List<string> EnsureTruncatedIgnoreTables { get; set; } = ["MigrationsHistory"];
}