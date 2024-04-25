// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

public class RequestModuleContextAccessor : IRequestModuleContextAccessor
{
    private readonly IEnumerable<IModule> modules;
    private readonly string[] pathSelectors = new[] { "/api/v", "/api" };

    public RequestModuleContextAccessor(
        IEnumerable<IModule> modules = null,
        string[] pathSelectors = null)
    {
        this.modules = modules.SafeNull();

        if (pathSelectors is not null)
        {
            this.pathSelectors = pathSelectors;
        }
    }

    public virtual IModule Find(HttpRequest request)
    {
        request.Headers.TryGetValue(ModuleConstants.ModuleNameKey, out var moduleName);

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            moduleName = request.Query[ModuleConstants.ModuleNameKey];
        }

        foreach (var pathSelector in this.pathSelectors.SafeNull())
        {
            if (string.IsNullOrWhiteSpace(moduleName) &&
                request.Path.Value.Contains(pathSelector, StringComparison.OrdinalIgnoreCase))
            {
                // TODO: source generated regex? api/MODULENAME/controller
                moduleName = request.Path.Value.SliceFrom(pathSelector);
                moduleName = moduleName.ToString().SliceFrom("/").SliceTill("/");
            }
        }

        return this.modules.FirstOrDefault(m => m.Name.SafeEquals(moduleName));
    }
}