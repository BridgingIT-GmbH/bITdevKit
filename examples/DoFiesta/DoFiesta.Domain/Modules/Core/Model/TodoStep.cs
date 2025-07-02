﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

using System.Diagnostics;
using DevKit.Domain.Model;

[DebuggerDisplay("Id={Id}, Status={Status}")]
[TypedEntityId<Guid>]
public class TodoStep : Entity<TodoStepId>
{
    public TodoItemId TodoItemId { get; set; }

    public string Description { get; set; }

    public TodoStatus Status { get; set; }

    public int OrderIndex { get; set; }
}
