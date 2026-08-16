// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines operations for i password validator.
/// </summary>
public interface IPasswordValidator
{
    /// <summary>
    /// Validates password.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="providedPassword">The provided password used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    bool ValidatePassword(FakeUser user, string providedPassword);
}

/// <summary>
/// Represents password validator.
/// </summary>
public class PasswordValidator : IPasswordValidator
{
    /// <summary>
    /// Validates password.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="providedPassword">The provided password used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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
