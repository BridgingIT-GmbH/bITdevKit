// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Projects approved metadata for a selected object without enabling capture.</summary>
/// <example><code>public AuditDescriptor DescribeAudit(Customer value) => AuditDescriptorBuilder.For&lt;Customer&gt;().Resource&lt;Customer&gt;(value.Id).Build();</code></example>
public interface IAuditDetailsProvider<in T>
{
    /// <summary>Returns an explicit metadata projection, never an entire object graph.</summary>
    /// <example><code>var descriptor = provider.DescribeAudit(customer);</code></example>
    AuditDescriptor DescribeAudit(T value);
}
