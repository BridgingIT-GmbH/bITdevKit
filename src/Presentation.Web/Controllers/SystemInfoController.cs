// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;

//[Authorize]
[ApiController]
[Route("api/_system/info")]
public class SystemInfoController : ControllerBase
{
    private readonly ILogger<SystemInfoController> logger;

    public SystemInfoController(ILogger<SystemInfoController> logger)
    {
        this.logger = logger;
    }

    [HttpGet]
    [OpenApiTag("_system/info")]
    public async Task<SystemInfo> Get()
    {
        this.logger.LogDebug("gathering system information");

        return new SystemInfo
        {
            Request = new Dictionary<string, object>
            {
                //["correlationId"] = this.HttpContext?.GetCorrelationId(),
                //["requestId"] = this.HttpContext?.GetRequestId(),
                ["isLocal"] = IsLocal(this.HttpContext?.Request),
                ["host"] = Dns.GetHostName(),
                ["ip"] = (await Dns.GetHostAddressesAsync(Dns.GetHostName()).AnyContext()).Select(i => i.ToString()).Where(i => i.Contains('.', StringComparison.OrdinalIgnoreCase)),
                //["userIdentity"] = this.HttpContext?.User?.Identity,
                //["username"] = this.HttpContext?.User?.Identity?.Name
            },
            Runtime = new Dictionary<string, string>
            {
                ["name"] = Assembly.GetEntryAssembly().GetName().Name,
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                ["version"] = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                //["versionFile"] = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version,
                ["versionInformation"] = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
                ["buildDate"] = GetBuildDate(Assembly.GetEntryAssembly()).ToString("o"),
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
    }

    private static bool IsLocal(HttpRequest source)
    {
        // https://stackoverflow.com/a/41242493/7860424
        var connection = source?.HttpContext?.Connection;
        if (IsIpAddressSet(connection?.RemoteIpAddress))
        {
            return IsIpAddressSet(connection.LocalIpAddress)
                //if local is same as remote, then we are local
                ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                //else we are remote if the remote IP address is not a loopback address
                : IPAddress.IsLoopback(connection.RemoteIpAddress);
        }

        return true;

        static bool IsIpAddressSet(IPAddress address)
        {
            return address is not null && address.ToString() != "::1";
        }
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        // origin: https://www.meziantou.net/2018/09/24/getting-the-date-of-build-of-a-net-assembly-at-runtime
        // note: project file needs to contain:
        //       <PropertyGroup><SourceRevisionId>build$([System.DateTime]::UtcNow.ToString("yyyyMMddHHmmss"))</SourceRevisionId></PropertyGroup>
        const string BuildVersionMetadataPrefix1 = "+build";
        const string BuildVersionMetadataPrefix2 = ".build"; // TODO: make this an array of allowable prefixes
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute?.InformationalVersion is not null)
        {
            var value = attribute.InformationalVersion;
            var prefix = BuildVersionMetadataPrefix1;
            var index = value.IndexOf(BuildVersionMetadataPrefix1, StringComparison.OrdinalIgnoreCase);
            // fallback for '.build' prefix
            if (index == -1)
            {
                prefix = BuildVersionMetadataPrefix2;
                index = value.IndexOf(BuildVersionMetadataPrefix2, StringComparison.OrdinalIgnoreCase);
            }

            if (index > 0)
            {
                value = value[(index + prefix.Length)..];
                if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }
        }

        return default;
    }
}
