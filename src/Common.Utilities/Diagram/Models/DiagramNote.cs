// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a reusable diagram note.
/// </summary>
/// <param name="TargetId">The target node identifier.</param>
/// <param name="Text">The note text.</param>
/// <param name="Position">The note position.</param>
public sealed record DiagramNote(
    string TargetId,
    string Text,
    DiagramNotePosition Position = DiagramNotePosition.Right);