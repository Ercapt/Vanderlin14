// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.River;

/// <summary>
/// Indicates this entity is currently touching a river current.
/// Kept separate from <c>ConveyedComponent</c> on purpose so the river
/// and conveyor systems never fight over the same state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RiverConveyedComponent : Component
{
    /// <summary>
    /// True while the current is actively pushing this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Conveying;
}
