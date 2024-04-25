// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency
using Newtonsoft.Json.Serialization;

public class PrivateSetterContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(
        MemberInfo member,
        MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        if (!property.Writable)
        {
            var propertyInfo = member as PropertyInfo;
            if (propertyInfo is not null)
            {
                property.Writable = propertyInfo.GetSetMethod(true) is not null;
            }
        }

        return property;
    }
}