// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

public interface IPasswordValidator
{
    bool ValidatePassword(FakeUser user, string providedPassword);
}

public class PasswordValidator : IPasswordValidator
{
    public bool ValidatePassword(FakeUser user, string providedPassword)
    {
        if (user == null) //  || string.IsNullOrEmpty(providedPassword)
        {
            return false;
        }

        // use simple comparison (no salt)
        return user.Password.SafeEquals(providedPassword);
    }
}