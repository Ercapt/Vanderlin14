// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.Leech;

/// <summary>
/// Marks swamp water that infests creatures with leeches.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SwampWaterComponent : Component
{
    /// <summary>
    /// Attach chance per check for barefoot creatures.
    /// </summary>
    [DataField]
    public float BarefootChance = 0.5f;

    /// <summary>
    /// Attach chance per check for creatures wearing shoes.
    /// </summary>
    [DataField]
    public float ShodChance = 0.3f;
}
