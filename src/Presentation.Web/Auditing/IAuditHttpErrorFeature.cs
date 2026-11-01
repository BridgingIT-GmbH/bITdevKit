// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>Lets a downstream exception handler publish safe error evidence to an enclosing audit boundary.</summary>
/// <example><code>context.Features.Get&lt;IAuditHttpErrorFeature&gt;()?.Observe(exception);</code></example>
public interface IAuditHttpErrorFeature
{
    /// <summary>Reports an observed exception without approving message, stack, request, or exception-data capture.</summary>
    /// <example><code>feature.Observe(exception, knownRejection: true);</code></example>
    void Observe(Exception exception, bool knownRejection = false);
}
