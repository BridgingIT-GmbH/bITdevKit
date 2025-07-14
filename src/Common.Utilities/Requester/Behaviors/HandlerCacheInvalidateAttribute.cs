// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerCacheInvalidateAttribute : Attribute
{
    public HandlerCacheInvalidateAttribute(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key), "Cache key cannot be null or empty.");
        }

        this.Key = key;
    }

    public string Key { get; }
}
