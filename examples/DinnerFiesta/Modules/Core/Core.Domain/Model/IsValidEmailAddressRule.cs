// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain.Model;

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain;

public class IsValidEmailAddressRule(string value) : IBusinessRule
{
    private static readonly Regex Regex = new( // TODO: change to compiled regex (source gen)
        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
        RegexOptions.Compiled);

    private readonly string value = value?.ToLowerInvariant();

    public string Message => "Not a valid email address";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(!string.IsNullOrEmpty(this.value) && this.value.Length <= 255 && Regex.IsMatch(this.value));
}

public static partial class EmailAddressRules
{
    public static IBusinessRule IsValid(string value) => new IsValidEmailAddressRule(value);
}