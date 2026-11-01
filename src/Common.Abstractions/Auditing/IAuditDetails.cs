// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Explicitly projects approved business metadata without enabling capture.</summary>
/// <example><code>public AuditDescriptor DescribeAudit() => AuditDescriptorBuilder.For&lt;Customer&gt;().Build();</code></example>
public interface IAuditDetails
{
    /// <summary>Returns a copied descriptor; never return a complete request or entity graph.</summary>
    /// <example><code>var descriptor = details.DescribeAudit();</code></example>
    AuditDescriptor DescribeAudit();
}
