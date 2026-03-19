// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using FluentValidation;

public class TodoItemExportMultiQuery : RequestBase<Stream>
{
    public class Validator : AbstractValidator<TodoItemExportMultiQuery>
    {
    }
}
