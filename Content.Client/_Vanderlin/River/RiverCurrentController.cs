// SPDX-License-Identifier: AGPL-3.0-or-later
// Client half of the river current: runs the shared prediction (UpdateBeforeSolve)
// so entities don't rubber-band while drifting. Mirrors the server controller.

using Content.Shared._Vanderlin.River;

namespace Content.Client._Vanderlin.River;

public sealed class RiverCurrentController : SharedRiverCurrentController
{
}
