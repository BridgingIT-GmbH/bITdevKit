// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Manages the context for request modules by providing functionality to
/// find the appropriate module based on the provided HTTP request.
/// </summary>
public class RequestModuleContextAccessor : IRequestModuleContextAccessor
{
    /// <summary>
    /// A variable representing a collection of modules within the application framework.
    /// Utilized within RequestModuleContextAccessor for providing functionalities such as finding
    /// the appropriate module based on a provided HTTP request.
    /// </summary>
    private readonly IEnumerable<IModule> modules;

    /// <summary>
    /// An array of string paths used to identify specific modules within HTTP requests.
    /// </summary>
    private readonly string[] pathSelectors = ["/api/v", "/api"];

    /// <summary>
    /// Provides functionality for accessing the appropriate module based on the provided HTTP request.
    /// </summary>
    public RequestModuleContextAccessor(IEnumerable<IModule> modules = null, string[] pathSelectors = null)
    {
        this.modules = modules.SafeNull();

        if (pathSelectors is not null)
        {
            this.pathSelectors = pathSelectors;
        }
    }

    /// <summary>
    /// Finds the appropriate module based on the provided HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request used to determine the module.</param>
    /// <returns>The module associated with the HTTP request, or null if no module is found.</returns>
    public virtual IModule Find(HttpRequest request)
    {
        request.Headers.TryGetValue(ModuleConstants.ModuleNameKey, out var moduleName);

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            moduleName = request.Query[ModuleConstants.ModuleNameKey];
        }

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            foreach (var module in this.modules) // check if modulename is part of path
            {
                if (request.Path.Value != null &&
                    request.Path.Value.Contains($"/{module.Name}/", StringComparison.OrdinalIgnoreCase))
                {
                    moduleName = module.Name;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            foreach (var pathSelector in this.pathSelectors.SafeNull()) // check if module is found with the path selectors
            {
                if (string.IsNullOrWhiteSpace(moduleName) &&
                    request.Path.Value.Contains(pathSelector, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: source generated regex? api/MODULENAME/controller
                    moduleName = request.Path.Value.SliceFrom(pathSelector);
                    moduleName = moduleName.ToString().SliceFrom("/").SliceTill("/");
                }
            }
        }

        return this.modules.FirstOrDefault(m => m.Name.SafeEquals(moduleName));
    }
}