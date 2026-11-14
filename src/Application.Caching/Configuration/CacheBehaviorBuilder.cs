// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records complete ordered optional behavior lists for the selected cache stages.</summary>
/// <example><code>behaviors.CollectionOperations("logging", "metrics").SourceExecution("logging");</code></example>
public sealed class CacheBehaviorBuilder : PatchOptionsBuilder<CacheBehaviorOptions, CacheBehaviorBuilder>
{
    /// <summary>Selects the ordered collection operation behavior aliases.</summary>
    /// <param name="aliases">The complete ordered code-registered behavior list.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>behaviors.CollectionOperations("logging", "metrics");</code></example>
    public CacheBehaviorBuilder CollectionOperations(params string[] aliases) => this.With(options => options.CollectionOperations, aliases);

    /// <summary>Selects the ordered owned-source behavior aliases.</summary>
    /// <param name="aliases">The complete ordered code-registered behavior list.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>behaviors.SourceExecution("logging");</code></example>
    public CacheBehaviorBuilder SourceExecution(params string[] aliases) => this.With(options => options.SourceExecution, aliases);

    /// <summary>Selects the ordered staged-payload transformation behavior aliases.</summary>
    /// <param name="aliases">The complete ordered code-registered behavior list.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>behaviors.PayloadTransformation("compression");</code></example>
    public CacheBehaviorBuilder PayloadTransformation(params string[] aliases) => this.With(options => options.PayloadTransformation, aliases);
}