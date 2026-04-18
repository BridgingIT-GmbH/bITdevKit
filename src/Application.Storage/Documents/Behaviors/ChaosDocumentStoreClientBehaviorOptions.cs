// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures fault injection for <see cref="ChaosDocumentStoreClientBehavior{T}" />.
/// </summary>
public class ChaosDocumentStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets a value between <c>0</c> and <c>1</c> that determines how often a fault is injected.
    /// </summary>
    public double InjectionRate { get; set; }

    /// <summary>
    /// Gets or sets the exception injected when the chaos policy is triggered.
    /// </summary>
    public Exception Fault { get; set; } = new ChaosException();
}
