﻿//// MIT-License
//// Copyright BridgingIT GmbH - All Rights Reserved
//// Use of this source code is governed by an MIT-style license that can be
//// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Application.Commands;

//using System;
//using FluentValidation;

//public abstract class CommandValidator<TCommand> : AbstractValidator<TCommand>
//    where TCommand : class //, ICommandRequest
//{
//    protected bool NotBeNullOrEmpty(string value)
//    {
//        return !string.IsNullOrEmpty(value);
//    }

//    protected bool BeValidGuid(string value)
//    {
//        return Guid.TryParse(value, out _);
//    }

//    protected bool BeEmptyGuid(string value)
//    {
//        return string.IsNullOrEmpty(value) || (Guid.TryParse(value, out var guid) && guid == Guid.Empty);
//    }

//    protected bool NotBeEmptyGuid(string value)
//    {
//        return !string.IsNullOrEmpty(value) && Guid.TryParse(value, out var guid) && guid != Guid.Empty;
//    }
//}

