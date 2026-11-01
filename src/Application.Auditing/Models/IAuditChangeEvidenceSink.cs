// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Optional recorder capability for copied source change evidence and observable transaction completion.</summary>
/// <example><code>var handle = (scope as IAuditChangeEvidenceSink)?.StageChange(evidence);</code></example>
public interface IAuditChangeEvidenceSink
{
    /// <summary>Admits bounded copied evidence as Unknown until its source commit can be confirmed.</summary>
    /// <example><code>var pending = sink.StageChange(sourceSnapshot);</code></example>
    AuditChangeHandle StageChange(AuditChangeEvidence evidence);
}
