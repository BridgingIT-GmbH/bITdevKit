// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class TraceActivityAttribute : Attribute
{
    public TraceActivityAttribute(string name = null, bool recordExceptions = true)
    {
        this.Name = name;
        this.RecordExceptions = recordExceptions;
    }

    public string Name { get; }

    public bool RecordExceptions { get; }
}