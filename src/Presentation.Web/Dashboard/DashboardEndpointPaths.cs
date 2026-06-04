// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

public class DashboardEndpointPaths
{
    public string Identity { get; set; } = "/identity";

    public string IdentityClientCredentialsLogin { get; set; } = "/identity/client-credentials/login";

    public string Metrics { get; set; } = "/metrics";

    public string MetricsContent { get; set; } = "/metrics/content";
}
