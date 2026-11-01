// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Current read restrictions applied after original-source verification.</summary>
/// <example><code>var exposure = new AuditExposureOptions { ExposeChangeValues = true, Revision = "approved-v2" };</code></example>
public sealed record AuditExposureOptions
{
    /// <summary>Whether confirmed committed change values may pass the current capture policy.</summary>
    /// <example><code>var allowed = exposure.ExposeChangeValues;</code></example>
    public bool ExposeChangeValues { get; init; }

    /// <summary>Host revision bound into export/read profiles when exposure changes.</summary>
    /// <example><code>var revision = exposure.Revision;</code></example>
    public string Revision { get; init; } = "default-v1";
}
