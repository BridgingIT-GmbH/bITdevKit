// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Validates current trusted host scope before an adapter consumes a body or an Application handler accesses a collection.</summary>
/// <example><code>var authorized = validator.Validate(access, "cards", CacheAccessActions.Read);</code></example>
public sealed class CacheManagementAccessValidator(CacheCatalog catalog)
{
    private static readonly CacheAccessContextValidator ContextValidator = new();

    /// <summary>Checks namespace, action, bounded identity metadata, declared collection, and any host-authorized collection subset.</summary>
    public Result Validate(CacheAccessContext access, string collection, CacheAccessActions action, bool allowGlobal = false)
    {
        if (access is null) { return Result.Failure(new UnauthorizedError("Trusted cache access context is required.")); }

        if (!ContextValidator.Validate(access).IsValid || action is not (CacheAccessActions.Read or CacheAccessActions.Write or CacheAccessActions.Invalidate))
        {
            return Result.Failure(new CacheError(CacheErrorCode.Validation, "Trusted cache access metadata exceeds its declared bounds."));
        }

        if (!StringComparer.OrdinalIgnoreCase.Equals(access.Namespace, catalog.Options.Namespace) || (access.Actions & action) != action ||
            (collection is not null && access.Collections is not null && !access.Collections.Contains(collection, StringComparer.OrdinalIgnoreCase)))
        {
            return Result.Failure(new UnauthorizedError("The current host identity is not authorized for this cache scope."));
        }

        if (collection is null)
        {
            return allowGlobal ? Result.Success() : Result.Failure(new CacheError(CacheErrorCode.Validation, "An explicit declared collection is required."));
        }

        try
        {
            return catalog.Collections.ContainsKey(CacheIdentityNormalizer.Name(collection)) ? Result.Success() :
                Result.Failure(new CacheError(CacheErrorCode.Contract, "The cache collection is not declared."));
        }
        catch (ArgumentException)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Validation, "The cache identity is invalid."));
        }
    }
}