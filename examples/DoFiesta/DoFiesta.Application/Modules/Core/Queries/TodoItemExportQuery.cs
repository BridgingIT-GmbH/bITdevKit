// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using FluentValidation;

public class TodoItemExportQuery(Format format) : RequestBase<Stream>
{
    public Format Format { get; } = format;

    public class Validator : AbstractValidator<TodoItemExportQuery>
    {
        public Validator()
        {
            this.RuleFor(x => x.Format)
                .Must(x => x.IsBuiltIn())
                .WithMessage("Invalid export format");
        }
    }
}
