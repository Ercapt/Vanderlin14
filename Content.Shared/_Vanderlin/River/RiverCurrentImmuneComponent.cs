// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.River;

/// <summary>
/// Entities with this marker are ignored by the river current.
/// Used for things like fishing lures that must stay where they were cast.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RiverCurrentImmuneComponent : Component
{
}
