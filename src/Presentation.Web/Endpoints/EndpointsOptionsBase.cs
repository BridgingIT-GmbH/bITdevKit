// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public abstract class EndpointsOptionsBase
{
    public bool Enabled { get; set; } = true;

    public string GroupPath { get; set; } = "/api";

    public string GroupTag { get; set; } = string.Empty;

    public bool RequireAuthorization { get; set; }

    public bool ExcludeFromDescription { get; set; }

    public string[] RequireRoles { get; set; } = []; // roles

    public string RequirePolicy { get; set; }
}