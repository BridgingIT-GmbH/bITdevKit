// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

public class PropertyBackingFieldContractResolver : DefaultContractResolver
{
    /// <summary>
    /// Properly deserialize readonly private backing fields used for immutable Collections.
    /// Otherwise the deserializer will ignore the collection and it will be empty.
    ///
    /// <code>
    /// public class System
    /// {
    ///     private readonly IList<User> users = new List<User>();
    ///
    ///     public IEnumerable<User> Users => this.users.ToList().AsReadOnly();
    ///
    ///     public void AddUser(User user)
    ///     {
    ///         this.users.Add(user);
    ///     }
    /// }
    /// </code>
    /// </summary>
    /// <param name="member"></param>
    /// <param name="memberSerialization"></param>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        if (!property.Writable)
        {
            var propertyInfo = member as PropertyInfo;
            if (propertyInfo != null)
            {
                property.Writable = propertyInfo.GetSetMethod(true) != null;
                if (!property.Writable)
                {
                    var privateField = member.DeclaringType.GetRuntimeFields()
                        .FirstOrDefault(x => x.Name.Equals(char.ToLowerInvariant(property.PropertyName[0]) + property.PropertyName[1..]));
                    if (privateField != null)
                    {
                        var originalPropertyName = property.PropertyName;
                        property = base.CreateProperty(privateField, memberSerialization);
                        property.Writable = true;
                        property.PropertyName = originalPropertyName;
                        property.UnderlyingName = originalPropertyName;
                        property.Readable = true;
                    }
                }
            }
        }

        return property;
    }
}