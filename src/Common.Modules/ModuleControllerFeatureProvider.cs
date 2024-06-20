// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;

public class ModuleControllerFeatureProvider(
    IEnumerable<IModuleContextAccessor> moduleAccessors) : ControllerFeatureProvider
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;

    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!typeInfo.IsClass)
        {
            return false;
        }

        if (typeInfo.IsAbstract)
        {
            return false;
        }

        if (typeInfo.ContainsGenericParameters)
        {
            return false;
        }

        if (typeInfo.IsDefined(typeof(NonControllerAttribute)))
        {
            return false;
        }

        if (!typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) &&
            !typeInfo.IsDefined(typeof(ControllerAttribute)))
        {
            return false;
        }

        Log.Logger.Debug("{LogKey} controller provider CHECK (controller={ControllerType})", ModuleConstants.LogKey, typeInfo.Name);
        Console.WriteLine($"{ModuleConstants.LogKey} controller provider CHECK (controller={typeInfo.Name})");

        var module = this.moduleAccessors.Find(typeInfo);
#pragma warning disable RCS1146 // Use conditional access.
        if (module is not null && !module.Enabled)
        {
            return false; // controller part of module but module not enabled, don't allow controller
        }
#pragma warning restore RCS1146 // Use conditional access.

        Log.Logger.Debug("{LogKey} controller provider ADDED (controller={ControllerType}), module={ModuleName})", ModuleConstants.LogKey, typeInfo.Name, module?.Name);
        Console.WriteLine($"{ModuleConstants.LogKey} controller provider ADDED (controller={typeInfo.Name}, module={module?.Name})");

        return true;
    }
}