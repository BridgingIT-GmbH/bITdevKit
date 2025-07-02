// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class SystemEndpointsOptions : EndpointsOptionsBase
{
    public SystemEndpointsOptions()
    {
        this.GroupPath = "/api/_system";
        this.GroupTag = "_system";
    }

    public bool EchoEnabled { get; set; } = true;

    public bool InfoEnabled { get; set; } = true;

    public bool ModulesEnabled { get; set; } = true;

    public bool HideSensitiveInformation { get; set; }

    public Dictionary<string, string> CustomMetadata { get; set; } = [];
}