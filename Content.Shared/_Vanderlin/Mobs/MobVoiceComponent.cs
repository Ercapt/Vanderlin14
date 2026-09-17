// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Sound;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Vanderlin.Mobs;

/// <summary>
/// Vocalizations for simple mobs: aggro on acquiring a target,
/// pain on damage, death cry, idle noises.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MobVoiceComponent : Component
{
    [DataField]
    public ProtoId<SoundCollectionPrototype> AggroSounds = "VanderlinGatorAggro";

    [DataField]
    public ProtoId<SoundCollectionPrototype> PainSounds = "VanderlinGatorPain";

    [DataField]
    public ProtoId<SoundCollectionPrototype> DeathSounds = "VanderlinGatorDeath";

    [DataField]
    public ProtoId<SoundCollectionPrototype> IdleSounds = "VanderlinGatorIdle";

    /// <summary>
    /// Minimum delay between aggro barks.
    /// </summary>
    [DataField]
    public TimeSpan AggroCooldown = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Minimum delay between pain sounds.
    /// </summary>
    [DataField]
    public TimeSpan PainCooldown = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Idle sounds roughly within this range.
    /// </summary>
    [DataField]
    public TimeSpan IdleMin = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan IdleMax = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan NextAggro = TimeSpan.Zero;

    [DataField]
    public TimeSpan NextPain = TimeSpan.Zero;

    [DataField]
    public TimeSpan NextIdle = TimeSpan.Zero;

    [DataField]
    public EntityUid? LastTarget;
}
