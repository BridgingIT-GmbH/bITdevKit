// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class SystemEndpoints(SystemEndpointsOptions options = null) : EndpointsBase
{
    private readonly SystemEndpointsOptions options = options ?? new SystemEndpointsOptions();

    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = app.MapGroup(this.options.GroupPrefix)
            .WithTags(this.options.GroupTag);

        if (this.options.RequireAuthorization)
        {
            group.RequireAuthorization();
        }

        group.MapGet(string.Empty, this.GetSystem)
                //.AllowAnonymous()
                .Produces<Dictionary<string, string>>(200)
                .Produces<ProblemDetails>(500);

        if (this.options.EchoEnabled)
        {
            group.MapGet("echo", this.GetEcho)
                //.AllowAnonymous()
                .Produces<string>(200)
                .Produces<ProblemDetails>(500);
        }

        if (this.options.InfoEnabled)
        {
            group.MapGet("info", this.GetInfo)
                //.AllowAnonymous()
                .Produces<SystemInfo>(200)
                .Produces<ProblemDetails>(500);
        }

        if (this.options.ModulesEnabled)
        {
            group.MapGet("modules", this.GetModules)
                //.AllowAnonymous()
                .Produces<IEnumerable<IModule>>(200)
                .Produces<ProblemDetails>(500);
        }
    }

    public IResult GetSystem(HttpContext httpContext)
    {
        var result = new Dictionary<string, string>();
        var host = $"{httpContext.Request.Scheme}://{httpContext.Request.Host.Value.Trim('/')}";
        if (this.options.EchoEnabled)
        {
            result.Add("echo", $"{host}/{this.options.GroupPrefix.Trim('/')}/echo");
        }

        if (this.options.InfoEnabled)
        {
            result.Add("info", $"{host}/{this.options.GroupPrefix.Trim('/')}/info");
        }

        if (this.options.ModulesEnabled)
        {
            result.Add("modules", $"{host}/{this.options.GroupPrefix.Trim('/')}/modules");
        }

        return Results.Ok(result);
    }

    public async Task<IResult> GetEcho(IMediator mediator, HttpContext httpContext)
    {
        var response = await mediator.Send(new EchoQuery());
        return Results.Ok(response.Result);
    }

    public async Task<IResult> GetInfo(IMediator mediator, HttpContext httpContext)
    {
        var result = new SystemInfo
        {
            Request = new Dictionary<string, object>
            {
                ["isLocal"] = IsLocal(httpContext?.Request),
                ["host"] = Dns.GetHostName(),
                ["ip"] = (await Dns.GetHostAddressesAsync(Dns.GetHostName())).Select(i => i.ToString()).Where(i => i.Contains('.')),
            },
            Runtime = new Dictionary<string, string>
            {
                ["name"] = Assembly.GetEntryAssembly().GetName().Name,
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                ["version"] = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                ["versionInformation"] = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
                ["buildDate"] = Assembly.GetEntryAssembly().GetBuildDate().ToString("o"),
                ["processName"] = Process.GetCurrentProcess().ProcessName.Equals("dotnet", StringComparison.InvariantCultureIgnoreCase) ? $"{Process.GetCurrentProcess().ProcessName} (kestrel)" : Process.GetCurrentProcess().ProcessName,
                ["process64Bits"] = Environment.Is64BitProcess.ToString(),
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["runtime"] = RuntimeInformation.RuntimeIdentifier,
                ["machineName"] = Environment.MachineName,
                ["processorCount"] = Environment.ProcessorCount.ToString(),
                ["osDescription"] = RuntimeInformation.OSDescription,
                ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString()
            }
        };

        return Results.Ok(result);
    }

    public IResult GetModules(IEnumerable<IModule> modules)
    {
        return Results.Ok(modules);
    }

    private static bool IsLocal(HttpRequest source)
    {
        // https://stackoverflow.com/a/41242493/7860424
        var connection = source?.HttpContext?.Connection;
        if (IsIpAddressSet(connection?.RemoteIpAddress))
        {
            return IsIpAddressSet(connection.LocalIpAddress)
                ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress) //if local is same as remote, then we are local
                : IPAddress.IsLoopback(connection.RemoteIpAddress); //else we are remote if the remote IP address is not a loopback address
        }

        return true;

        static bool IsIpAddressSet(IPAddress address)
        {
            return address is not null && address.ToString() != "::1";
        }
    }
}