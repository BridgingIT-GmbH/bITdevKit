// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using BridgingIT.DevKit.Common;

public class DatabaseMigratorOptionsBuilder :
    OptionsBuilderBase<DatabaseMigratorOptions, DatabaseMigratorOptionsBuilder>
{
    public DatabaseMigratorOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    public DatabaseMigratorOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;
        return this;
    }

    public DatabaseMigratorOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;
        return this;
    }

    public DatabaseMigratorOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public DatabaseMigratorOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);
        return this;
    }

    public DatabaseMigratorOptionsBuilder DeleteOnStartup(bool value = true)
    {
        this.Target.EnsureDeleted = value;
        return this;
    }
}