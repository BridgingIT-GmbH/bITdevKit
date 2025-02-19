// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Application.Queries;
using Common;
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

        var group = this.MapGroup(app, this.options);

        group.MapGet(string.Empty, this.GetSystem)
            //.AllowAnonymous()
            .Produces<Dictionary<string, string>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        if (this.options.EchoEnabled)
        {
            group.MapGet("echo", this.GetEcho)
                //.AllowAnonymous()
                .Produces<string>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
        }

        if (this.options.InfoEnabled)
        {
            group.MapGet("info", this.GetInfo)
                //.AllowAnonymous()
                .Produces<SystemInfo>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
        }

        if (this.options.ModulesEnabled)
        {
            group.MapGet("modules", this.GetModules)
                //.AllowAnonymous()
                .Produces<IEnumerable<SystemModule>>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
        }
    }

    public IResult GetSystem(HttpContext httpContext)
    {
        var result = new Dictionary<string, string>();
        var host = $"{httpContext.Request.Scheme}://{httpContext.Request.Host.Value.Trim('/')}";
        if (this.options.EchoEnabled)
        {
            result.Add("echo", $"{host}/{this.options.GroupPath.Trim('/')}/echo");
        }

        if (this.options.InfoEnabled)
        {
            result.Add("info", $"{host}/{this.options.GroupPath.Trim('/')}/info");
        }

        if (this.options.ModulesEnabled)
        {
            result.Add("modules", $"{host}/{this.options.GroupPath.Trim('/')}/modules");
        }

        return Results.Ok(result);
    }

    public async Task<IResult> GetEcho(IMediator mediator, HttpContext httpContext)
    {
        var response = await mediator.Send(new EchoQuery());

        return Results.Ok(response.Result);
    }

    public async Task<IResult> GetInfo(IMediator mediator, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = new SystemInfo
        {
            Request =
                new Dictionary<string, object>
                {
                    ["isLocal"] = !this.options.HideSensitiveInformation ? IsLocal(httpContext?.Request) : string.Empty,
                    ["host"] = !this.options.HideSensitiveInformation ? Dns.GetHostName() : string.Empty,
                    ["ip"] = !this.options.HideSensitiveInformation ? (await Dns.GetHostAddressesAsync(Dns.GetHostName(), cancellationToken)).Select(i => i.ToString()).Where(i => i.Contains('.')) : string.Empty
                },
            Runtime = new Dictionary<string, string>
            {
                ["name"] = Assembly.GetEntryAssembly().GetName().Name,
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                ["version"] = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                ["versionInformation"] = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
                ["buildDate"] = Assembly.GetEntryAssembly().GetBuildDate().ToString("o"),
                ["processName"] = !this.options.HideSensitiveInformation ? Process.GetCurrentProcess().ProcessName.Equals("dotnet", StringComparison.InvariantCultureIgnoreCase) ? $"{Process.GetCurrentProcess().ProcessName} (kestrel)" : Process.GetCurrentProcess().ProcessName : string.Empty,
                ["process64Bits"] = !this.options.HideSensitiveInformation ? Environment.Is64BitProcess.ToString() : string.Empty,
                ["framework"] = !this.options.HideSensitiveInformation ? RuntimeInformation.FrameworkDescription : string.Empty,
                ["runtime"] = !this.options.HideSensitiveInformation ? RuntimeInformation.RuntimeIdentifier : string.Empty,
                ["machineName"] = !this.options.HideSensitiveInformation ? Environment.MachineName : string.Empty,
                ["processorCount"] = !this.options.HideSensitiveInformation ? Environment.ProcessorCount.ToString() : string.Empty,
                ["osDescription"] = !this.options.HideSensitiveInformation ? RuntimeInformation.OSDescription : string.Empty,
                ["osArchitecture"] = !this.options.HideSensitiveInformation ? RuntimeInformation.OSArchitecture.ToString() : string.Empty
            }
        };

        return Results.Ok(result);
    }

    public IResult GetModules(IEnumerable<SystemModule> modules)
    {
        return Results.Ok(modules.Select(e =>
        new SystemModule { Enabled = e.Enabled, IsRegistered = e.IsRegistered, Name = e.Name, Priority = e.Priority }));
    }

    private static bool IsLocal(HttpRequest source)
    {
        // https://stackoverflow.com/a/41242493/7860424
        var connection = source?.HttpContext?.Connection;
        if (IsIpAddressSet(connection?.RemoteIpAddress))
        {
            return IsIpAddressSet(connection.LocalIpAddress)
                ? connection.RemoteIpAddress.Equals(connection
                    .LocalIpAddress) //if local is same as remote, then we are local
                : IPAddress.IsLoopback(connection
                    .RemoteIpAddress); //else we are remote if the remote IP address is not a loopback address
        }

        return true;

        static bool IsIpAddressSet(IPAddress address)
        {
            return address is not null && address.ToString() != "::1";
        }
    }
}