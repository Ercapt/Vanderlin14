// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Vanderlin.Drown;

/// <summary>
/// Server-side state for an entity that is currently drowning.
/// Added when a prone entity is in drowning water, removed when it leaves.
/// </summary>
[RegisterComponent]
public sealed partial class DrowningComponent : Component
{
    /// <summary>
    /// When the next damage tick may apply.
    /// </summary>
    [DataField]
    public TimeSpan NextDamage = TimeSpan.Zero;

    /// <summary>
    /// When the next drowning sound may play.
    /// </summary>
    [DataField]
    public TimeSpan NextSound = TimeSpan.Zero;
}
