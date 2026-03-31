// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation error in pipeline registration, definition, or execution setup.
/// </summary>
/// <param name="message">The validation message.</param>
public class PipelineDefinitionValidationException(string message) : Exception(message);
