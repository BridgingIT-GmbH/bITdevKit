// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Exception that is thrown when a requested module is not enabled.
/// </summary>
public class ModuleNotEnabledException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModuleNotEnabledException" /> class.
    ///     Exception thrown when a module is not enabled but is required for an operation.
    /// </summary>
    public ModuleNotEnabledException(string moduleName)
        : base($"Module {moduleName} not enabled.") { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModuleNotEnabledException" /> class.
    ///     Represents an exception that is thrown when an attempt is made to use a module that is not enabled.
    /// </summary>
    public ModuleNotEnabledException(string moduleName, Exception innerException)
        : base($"Module {moduleName} not enabled.", innerException) { }
}