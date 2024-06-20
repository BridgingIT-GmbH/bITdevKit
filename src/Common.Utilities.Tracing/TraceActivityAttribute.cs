// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class TraceActivityAttribute(string name = null, bool recordExceptions = true) : Attribute
{
    public string Name { get; } = name;

    public bool RecordExceptions { get; } = recordExceptions;
}