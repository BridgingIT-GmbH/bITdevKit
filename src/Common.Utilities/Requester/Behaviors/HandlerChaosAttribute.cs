// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerChaosAttribute : Attribute
{
    public HandlerChaosAttribute(double injectionRate, bool enabled = true)
    {
        if (injectionRate < 0 || injectionRate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(injectionRate), "Injection rate must be between 0 and 1.");
        }

        this.InjectionRate = injectionRate;
        this.Enabled = enabled;
    }

    public double InjectionRate { get; }

    public bool Enabled { get; } = true;
}
