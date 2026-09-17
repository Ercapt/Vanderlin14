// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.Leech;

/// <summary>
/// A leech item. Hungry leeches can attach, fed ones (full of blood) cannot.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeechComponent : Component
{
    /// <summary>
    /// True once the leech drank its fill (hung the full duration).
    /// Fed leeches refuse to attach again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Fed;
}
