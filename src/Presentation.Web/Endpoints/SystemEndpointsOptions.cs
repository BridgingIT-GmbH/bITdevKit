// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Configures the built-in system endpoint set.
/// </summary>
/// <remarks>
///     The default group path is <c>/_bdk/api/system</c> and the default tag is <c>_bdk.System</c>. The root system route is
///     mapped whenever the endpoint set is enabled, while the echo, info, and modules routes can be enabled or disabled
///     independently.
///
///     Example:
///     <code>
///     var options = new SystemEndpointsOptions
///     {
///         InfoEnabled = true,
///         ModulesEnabled = false,
///         HideSensitiveInformation = true
///     };
///     </code>
/// </remarks>
public class SystemEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemEndpointsOptions" /> class with the default system group path
    ///     and OpenAPI tag.
    /// </summary>
    /// <remarks>
    ///     The constructor sets <see cref="EndpointsOptionsBase.GroupPath" /> to <c>/_bdk/api/system</c> and
    ///     <see cref="EndpointsOptionsBase.GroupTag" /> to <c>_bdk.System</c>.
    /// </remarks>
    public SystemEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/system";
        this.GroupTag = "_bdk.System";
        this.RouteNamePrefix = "_bdk.System";
    }

    /// <summary>
    ///     Gets or sets whether the <c>echo</c> route is mapped.
    /// </summary>
    /// <remarks>
    ///     When enabled, <see cref="SystemEndpoints.GetSystem" /> includes an <c>echo</c> link and
    ///     <see cref="SystemEndpoints.Map" /> maps the echo route. The default value is <c>true</c>.
    /// </remarks>
    public bool EchoEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether the <c>info</c> route is mapped.
    /// </summary>
    /// <remarks>
    ///     When enabled, <see cref="SystemEndpoints.GetSystem" /> includes an <c>info</c> link and
    ///     <see cref="SystemEndpoints.Map" /> maps the info route. The default value is <c>true</c>.
    /// </remarks>
    public bool InfoEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether the <c>modules</c> route is mapped.
    /// </summary>
    /// <remarks>
    ///     When enabled, <see cref="SystemEndpoints.GetSystem" /> includes a <c>modules</c> link and
    ///     <see cref="SystemEndpoints.Map" /> maps the modules route. The default value is <c>true</c>.
    /// </remarks>
    public bool ModulesEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether sensitive runtime and host details are hidden from the info response.
    /// </summary>
    /// <remarks>
    ///     When enabled, <see cref="SystemEndpoints.GetInfo" /> replaces host, IP, process, framework, machine, operating
    ///     system, memory, URL, and timezone values with empty strings. Custom metadata and uptime remain visible.
    /// </remarks>
    public bool HideSensitiveInformation { get; set; }

    /// <summary>
    ///     Gets or sets custom metadata included in the system info response.
    /// </summary>
    /// <remarks>
    ///     The dictionary is returned as <see cref="SystemInfo.CustomMetadata" /> by <see cref="SystemEndpoints.GetInfo" />.
    ///     It can be used for non-sensitive application or deployment information that should be exposed through the system
    ///     endpoint.
    /// </remarks>
    public Dictionary<string, string> CustomMetadata { get; set; } = [];
}