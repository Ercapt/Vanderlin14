// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.River;

/// <summary>
/// Makes the entity push things that touch it, like a conveyor belt,
/// but without any power requirement. Intended for river current.
/// Flow direction = entity rotation + <see cref="Angle"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RiverCurrentComponent : Component
{
    /// <summary>
    /// Angle to move entities by, relative to the owner's rotation.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public Angle Angle = Angle.Zero;

    /// <summary>
    /// How fast the current pushes, in units per second.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public float Speed = 2.5f;

    /// <summary>
    /// If false, the current does nothing. Always true unless disabled on purpose.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
