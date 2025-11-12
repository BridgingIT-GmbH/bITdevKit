// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Configuration options for the validation behavior, specifying when validation should apply.
/// </summary>
public class ActiveEntityValidatorBehaviorOptions
{
    /// <summary>
    /// Gets or sets when the validation should be applied (Insert, Update, Delete, or Upsert).
    /// </summary>
    public ApplyOn ApplyOn { get; set; } = ApplyOn.Upsert;

    /// <summary>
    /// Configures the validator to apply only on insert operations.
    /// </summary>
    /// <returns>The current options instance for fluent configuration.</returns>
    public ActiveEntityValidatorBehaviorOptions ApplyOnInsert()
    {
        this.ApplyOn = ApplyOn.Insert;
        return this;
    }

    /// <summary>
    /// Configures the validator to apply only on update operations.
    /// </summary>
    /// <returns>The current options instance for fluent configuration.</returns>
    public ActiveEntityValidatorBehaviorOptions ApplyOnUpdate()
    {
        this.ApplyOn = ApplyOn.Update;
        return this;
    }

    /// <summary>
    /// Configures the validator to apply on both insert and update operations.
    /// </summary>
    /// <returns>The current options instance for fluent configuration.</returns>
    public ActiveEntityValidatorBehaviorOptions ApplyOnUpsert()
    {
        this.ApplyOn = ApplyOn.Upsert;
        return this;
    }

    /// <summary>
    /// Configures the validator to apply only on delete operations.
    /// </summary>
    /// <returns>The current options instance for fluent configuration.</returns>
    public ActiveEntityValidatorBehaviorOptions ApplyOnDelete()
    {
        this.ApplyOn = ApplyOn.Delete;
        return this;
    }
}
