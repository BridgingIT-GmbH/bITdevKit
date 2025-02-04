// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

public class TodoStepModel
{
    public string Id { get; set; }

    public string TodoItemId { get; set; }

    public string Description { get; set; }

    public int Status { get; set; }

    public int OrderIndex { get; set; }

    public bool? IsDeleted { get; set; }
}
