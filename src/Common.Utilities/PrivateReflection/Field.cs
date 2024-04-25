// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.PrivateReflection;

using System.Reflection;

internal class Field : IProperty
{
    string IProperty.Name
    {
        get
        {
            return this.FieldInfo.Name;
        }
    }

    internal FieldInfo FieldInfo { get; set; }

    object IProperty.GetValue(object obj, object[] index)
    {
        return this.FieldInfo.GetValue(obj);
    }

    void IProperty.SetValue(object obj, object val, object[] index)
    {
        this.FieldInfo.SetValue(obj, val);
    }
}