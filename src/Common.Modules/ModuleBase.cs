// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public abstract class ModuleBase : IModule
{
    protected ModuleBase()
        : this(null) { }

    protected ModuleBase(string name, int priority = 99)
    {
        this.Name = name ??
            this.GetType().Name.Replace("Module", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        this.Priority = priority;
    }

    public bool Enabled { get; set; } = true;

    public string Name { get; }

    public int Priority { get; } = 99;

    public bool IsRegistered { get; set; }

    public virtual IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return services;
    }

    public virtual IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return app;
    }
}