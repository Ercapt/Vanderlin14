// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Sound;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Vanderlin.Glowshroom;

/// <summary>
/// A plant that shocks whoever steps on it: knocks down and paralyzes
/// for <see cref="StunTime"/> with no direct damage.
/// In water regions this is lethal, as the victim can't get its face out.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GlowshroomComponent : Component
{
    /// <summary>
    /// How long the victim is stunned (and down).
    /// </summary>
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Cooldown between shocks.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan NextZap = TimeSpan.Zero;

    [DataField]
    public ProtoId<SoundCollectionPrototype> StepSounds = "VanderlinPlantcross";
}
