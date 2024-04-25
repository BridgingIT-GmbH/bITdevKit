// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public interface IOptionsBuilder
{
    /// <summary>
    /// Gets the target.
    /// </summary>
    /// <value>
    /// The target.
    /// </value>
    object Target { get; }
}

public interface IOptionsBuilder<out T> : IOptionsBuilder
{
    T Build();
}