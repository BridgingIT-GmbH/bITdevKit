// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.PrivateReflection;

internal interface IProperty
{
    string Name { get; }

    object GetValue(object obj, object[] index);

    void SetValue(object obj, object val, object[] index);
}