// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Configures the built-in system endpoint group.
/// </summary>
/// <remarks>
///     The options control which system routes are exposed and whether host, process, runtime, and network details are
///     included in the information response. The constructor sets the default group path to <c>/api/_system</c>.
/// </remarks>
public class SystemEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemEndpointsOptions" /> class with the default system route group.
    /// </summary>
    public SystemEndpointsOptions()
    {
        this.GroupPath = "/api/_system";
        this.GroupTag = "_system";
    }

    /// <summary>
    ///     Gets or sets whether the echo endpoint is mapped.
    /// </summary>
    public bool EchoEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether the system information endpoint is mapped.
    /// </summary>
    public bool InfoEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether the module listing endpoint is mapped.
    /// </summary>
    public bool ModulesEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether host, process, runtime, operating system, and network details are omitted from responses.
    /// </summary>
    public bool HideSensitiveInformation { get; set; }

    /// <summary>
    ///     Gets or sets additional metadata included in the system information response.
    /// </summary>
    public Dictionary<string, string> CustomMetadata { get; set; } = [];
}