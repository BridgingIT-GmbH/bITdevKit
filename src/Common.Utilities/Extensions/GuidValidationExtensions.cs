// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;

public static class GuidValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidGuid<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(BeValidGuid).WithMessage("The field must be a valid GUID");
    }

    public static IRuleBuilderOptions<T, string> MustNotBeEmptyGuid<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(guid => BeValidGuid(guid) && guid != Guid.Empty.ToString())
            .WithMessage("The GUID must not be empty");
    }

    public static IRuleBuilderOptions<T, string> MustNotBeDefaultOrEmptyGuid<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(guid =>
                BeValidGuid(guid) && guid != Guid.Empty.ToString() && guid != default(Guid).ToString())
            .WithMessage("The GUID must not be default or empty");
    }

    public static IRuleBuilderOptions<T, string> MustBeEmptyGuid<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(guid => string.IsNullOrEmpty(guid) || guid == Guid.Empty.ToString())
            .WithMessage("The GUID must be empty");
    }

    public static IRuleBuilderOptions<T, string> MustBeDefaultOrEmptyGuid<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(guid => string.IsNullOrEmpty(guid) ||
                guid == Guid.Empty.ToString() ||
                guid == default(Guid).ToString())
            .WithMessage("The GUID must be default, empty, or null");
    }

    public static IRuleBuilderOptions<T, string> MustBeInGuidFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Matches(@"^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$")
            .WithMessage("The field must be in a valid GUID format");
    }

    private static bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}