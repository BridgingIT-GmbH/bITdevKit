// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;

public class IsValidEmailAddressRule(string value) : RuleBase
{
    private static readonly Regex Regex = new(
        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
        RegexOptions.Compiled,
        new TimeSpan(0, 0, 3));

    private readonly string value = value?.ToLowerInvariant();

    public override string Message => "Not a valid email address";

    public override Result Execute()
    {
        return Result.SuccessIf(
            !string.IsNullOrEmpty(this.value) &&
            this.value.Length <= 255 &&
            Regex.IsMatch(this.value));
    }
}