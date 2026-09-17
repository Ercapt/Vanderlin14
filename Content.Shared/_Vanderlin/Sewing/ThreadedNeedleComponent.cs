// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.Sewing;

/// <summary>
/// A thorn with thread. Using it consumes the threading (used state),
/// thread can be applied again to re-thread.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThreadedNeedleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Used;
}
