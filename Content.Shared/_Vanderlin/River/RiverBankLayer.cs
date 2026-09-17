// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Vanderlin.River;

/// <summary>
/// Layer map keys for the river bank sprites.
/// Unlike <c>EdgeLayer</c> from icon smoothing, these are drawn inside
/// the river tile itself, with no offset onto neighbouring tiles.
/// Use in prototypes as <c>map: [ "enum.RiverBankLayer.South" ]</c>.
/// </summary>
[Serializable, NetSerializable]
public enum RiverBankLayer : byte
{
    South,
    East,
    North,
    West
}
