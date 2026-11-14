// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

/// <summary>Describes an optional task-title suggestion and its searchable reference metadata.</summary>
/// <param name="Title">The suggested title, which the user can edit before creating a task.</param>
/// <param name="Category">The development activity area.</param>
/// <param name="Technology">The associated technology or General for technology-independent work.</param>
/// <param name="Language">The language of the suggested title.</param>
/// <example><code>var suggestion = new TaskLookupModel("Review pull request", "Development", "General", "en");</code></example>
public sealed record TaskLookupModel(string Title, string Category, string Technology, string Language);