// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
///     Exposes diagnostic system endpoints for discovery, echo, runtime information, and registered modules.
/// </summary>
/// <remarks>
///     The endpoint set maps routes under <see cref="SystemEndpointsOptions.GroupPath" /> when the options are enabled.
///     Individual <c>echo</c>, <c>info</c>, and <c>modules</c> routes are controlled by their matching option flags. The
///     <c>info</c> endpoint can hide machine, process, network, and runtime details when sensitive information should not
///     be exposed.
///
///     Example:
///     <code>
///     services.AddEndpoints(new SystemEndpoints(new SystemEndpointsOptions
///     {
///         InfoEnabled = true,
///         HideSensitiveInformation = true
///     }));
///     </code>
/// </remarks>
public class SystemEndpoints(SystemEndpointsOptions options = null, ILogger<SystemEndpoints> logger = null) : EndpointsBase
{
    private readonly SystemEndpointsOptions options = options ?? new SystemEndpointsOptions();
    private readonly ILogger<SystemEndpoints> logger = logger;
    private readonly Stopwatch uptimeStopwatch = Stopwatch.StartNew();

    /// <summary>
    ///     Maps the enabled system routes into the supplied route builder.
    /// </summary>
    /// <param name="app">The route builder that receives the system endpoint mappings.</param>
    /// <remarks>
    ///     The method returns without mapping routes when <see cref="EndpointsOptionsBase.Enabled" /> is <c>false</c>. The
    ///     root system route is always mapped when enabled; <c>echo</c>, <c>info</c>, and <c>modules</c> are mapped only when
    ///     their corresponding option flags are enabled. Routes inherit the group configuration applied by
    ///     <see cref="EndpointsBase.MapGroup" />.
    /// </remarks>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet(string.Empty, this.GetSystem)
            .WithName(this.options, "Get")
            .Produces<Dictionary<string, string>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        if (this.options.EchoEnabled)
        {
            group.MapGet("echo", this.GetEcho)
                .WithName(this.options, "GetEcho")
                .Produces<string>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
        }

        if (this.options.InfoEnabled)
        {
            group.MapGet("info", this.GetInfo)
                .WithName(this.options, "GetInfo")
                .Produces<SystemInfo>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
        }

        if (this.options.ModulesEnabled)
        {
            group.MapGet("modules", this.GetModules)
                .WithName(this.options, "GetModules")
                .Produces<IEnumerable<SystemModule>>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    ///     Returns links to the enabled system endpoint routes for the current request host.
    /// </summary>
    /// <param name="httpContext">The current HTTP context used to build absolute endpoint URLs.</param>
    /// <returns>An HTTP 200 result containing a dictionary of enabled system route names and URLs.</returns>
    /// <remarks>
    ///     The response includes only links for routes enabled in the options. URLs are built from the request scheme,
    ///     request host, and configured group path.
    /// </remarks>
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

    /// <summary>
    ///     Handles the system echo route.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>An HTTP 200 result with no response body.</returns>
    /// <remarks>
    ///     The current implementation does not inspect the request and is intended as a lightweight availability probe for
    ///     the system endpoint group.
    /// </remarks>
    public IResult GetEcho(HttpContext httpContext)
    {
        return Results.Ok();
    }

    /// <summary>
    ///     Returns runtime, request, memory, configuration, custom metadata, and uptime information for the application.
    /// </summary>
    /// <param name="httpContext">The current HTTP context used to determine request-local information.</param>
    /// <param name="cancellationToken">A cancellation token used while resolving host addresses.</param>
    /// <returns>
    ///     An HTTP 200 result containing <see cref="SystemInfo" /> on success; otherwise an HTTP 500 problem result when
    ///     system information cannot be collected.
    /// </returns>
    /// <remarks>
    ///     The method collects process, runtime, operating system, memory, configuration, and uptime values. When
    ///     <see cref="SystemEndpointsOptions.HideSensitiveInformation" /> is enabled, machine, network, process, runtime,
    ///     memory, and configuration details are returned as empty strings while custom metadata and uptime are still
    ///     included. DNS lookup failures for host addresses are logged as warnings and represented as <c>N/A</c>. Unexpected
    ///     collection failures are logged as errors and returned as problem details.
    /// </remarks>
    public async Task<IResult> GetInfo(HttpContext httpContext, CancellationToken cancellationToken)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var result = new SystemInfo
            {
                Request = new Dictionary<string, object>
                {
                    ["isLocal"] = !this.options.HideSensitiveInformation ? IsLocal(httpContext?.Request) : string.Empty,
                    ["host"] = !this.options.HideSensitiveInformation ? Dns.GetHostName() : string.Empty,
                    ["ip"] = !this.options.HideSensitiveInformation ? (await this.GetHostAddressesAsync(Dns.GetHostName(), cancellationToken)) : string.Empty
                },
                Runtime = new Dictionary<string, string>
                {
                    ["name"] = Assembly.GetEntryAssembly().GetName().Name,
                    ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    ["version"] = Common.Version.Parse(Assembly.GetEntryAssembly()).ToString(VersionFormat.WithPrerelease),
                    ["versionFull"] = Common.Version.Parse(Assembly.GetEntryAssembly()).ToString(),
                    ["buildDate"] = Assembly.GetEntryAssembly().GetBuildDate().ToString("o"),
                    ["processName"] = !this.options.HideSensitiveInformation ? (process.ProcessName.Equals("dotnet", StringComparison.InvariantCultureIgnoreCase) ? $"{process.ProcessName} (kestrel)" : process.ProcessName) : string.Empty,
                    ["process64Bits"] = !this.options.HideSensitiveInformation ? Environment.Is64BitProcess.ToString() : string.Empty,
                    ["framework"] = !this.options.HideSensitiveInformation ? RuntimeInformation.FrameworkDescription : string.Empty,
                    ["runtime"] = !this.options.HideSensitiveInformation ? RuntimeInformation.RuntimeIdentifier : string.Empty,
                    ["machineName"] = !this.options.HideSensitiveInformation ? Environment.MachineName : string.Empty,
                    ["processorCount"] = !this.options.HideSensitiveInformation ? Environment.ProcessorCount.ToString() : string.Empty,
                    ["osDescription"] = !this.options.HideSensitiveInformation ? RuntimeInformation.OSDescription : string.Empty,
                    ["osArchitecture"] = !this.options.HideSensitiveInformation ? RuntimeInformation.OSArchitecture.ToString() : string.Empty
                },
                Memory = new Dictionary<string, string>
                {
                    ["workingSet"] = !this.options.HideSensitiveInformation ? $"{process.WorkingSet64 / 1024 / 1024} MB" : string.Empty,
                    ["privateMemory"] = !this.options.HideSensitiveInformation ? $"{process.PrivateMemorySize64 / 1024 / 1024} MB" : string.Empty,
                    ["gcTotalMemory"] = !this.options.HideSensitiveInformation ? $"{GC.GetTotalMemory(false) / 1024 / 1024} MB" : string.Empty
                },
                Configuration = new Dictionary<string, string>
                {
                    ["urls"] = !this.options.HideSensitiveInformation ? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "N/A" : string.Empty,
                    ["timezone"] = !this.options.HideSensitiveInformation ? TimeZoneInfo.Local.StandardName : string.Empty
                },
                CustomMetadata = this.options.CustomMetadata,
                Uptime = this.uptimeStopwatch.Elapsed.ToString(@"dd\.hh\:mm\:ss")
            };

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve system info.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "System Info Failed",
                Detail = "An error occurred while retrieving system information."
            });
        }
    }

    /// <summary>
    ///     Returns the registered system modules with their public state.
    /// </summary>
    /// <param name="modules">The modules available from dependency injection.</param>
    /// <returns>
    ///     An HTTP 200 result containing module name, priority, enabled state, and registration state; otherwise an HTTP 500
    ///     problem result when module projection fails.
    /// </returns>
    /// <remarks>
    ///     The response projects each supplied module into a new <see cref="SystemModule" /> instance and does not expose
    ///     additional module implementation details. Exceptions are logged and converted to problem details.
    /// </remarks>
    public IResult GetModules(IEnumerable<SystemModule> modules)
    {
        try
        {
            return Results.Ok(modules.Select(e =>
                new SystemModule { Enabled = e.Enabled, IsRegistered = e.IsRegistered, Name = e.Name, Priority = e.Priority }));
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve system modules.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Modules Retrieval Failed",
                Detail = "An error occurred while retrieving system modules."
            });
        }
    }

    private static bool IsLocal(HttpRequest source)
    {
        var connection = source?.HttpContext?.Connection;
        if (IsIpAddressSet(connection?.RemoteIpAddress))
        {
            return IsIpAddressSet(connection.LocalIpAddress)
                ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                : IPAddress.IsLoopback(connection.RemoteIpAddress);
        }

        return true;

        static bool IsIpAddressSet(IPAddress address)
        {
            return address is not null && address.ToString() != "::1";
        }
    }

    private async Task<string> GetHostAddressesAsync(string hostName, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostName, cancellationToken);
            return string.Join(", ", addresses.Select(i => i.ToString()).Where(i => i.Contains('.')));
        }
        catch (Exception ex)
        {
            this.logger?.LogWarning(ex, "Failed to resolve host addresses for {HostName}.", hostName);
            return "N/A";
        }
    }
}