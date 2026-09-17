// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.Flora;

/// <summary>
/// A rummageable bush. Interacting rolls <see cref="ThornChance"/> to pull
/// a thorn, up to <see cref="RummagesLeft"/> rummages. Success on the first
/// try or running out of rummages depletes the bush (empty overlay, locked).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BushComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ThornChance = 0.3f;

    [DataField, AutoNetworkedField]
    public int RummagesLeft = 2;

    [DataField, AutoNetworkedField]
    public bool Depleted;
}
