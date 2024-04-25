// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Diagnostics;

[DebuggerDisplay("Id={Id}, Name={Name}")]
public class DinnerSnapshotDocument
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public MenuSnapshotDocument Menu { get; internal set; }
}

[DebuggerDisplay("Id={Id}, Name={Name}")]
public class MenuSnapshotDocument
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }
}