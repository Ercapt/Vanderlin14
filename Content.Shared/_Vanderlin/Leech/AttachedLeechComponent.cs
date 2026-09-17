// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.Leech;

/// <summary>
/// Marks a creature that currently has a leech hanging on its leg.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AttachedLeechComponent : Component
{
    /// <summary>
    /// Which leg the leech hangs on. Purely cosmetic (examine text).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LeftLeg = true;

    /// <summary>
    /// When the leech gets full and falls off by itself.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DetachAt = TimeSpan.Zero;
}
