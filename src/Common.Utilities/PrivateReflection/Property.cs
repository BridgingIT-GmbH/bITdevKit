// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.PrivateReflection;

using System.Reflection;

internal class Property : IProperty
{
    object IProperty.GetValue(object obj, object[] index)
    {
        return this.PropertyInfo.GetValue(obj, index);
    }

    void IProperty.SetValue(object obj, object val, object[] index)
    {
        this.PropertyInfo.SetValue(obj, val, index);
    }
#pragma warning disable SA1202
#pragma warning disable SA1201
    internal PropertyInfo PropertyInfo { get; set; }
    string IProperty.Name => this.PropertyInfo.Name;
#pragma warning restore SA1201
#pragma warning restore SA1202
}