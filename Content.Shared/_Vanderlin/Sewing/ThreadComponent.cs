// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Vanderlin.Sewing;

/// <summary>
/// Plant fiber thread. Used on a thorn to craft a threaded needle,
/// or on a used needle to re-thread it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThreadComponent : Component
{
}
