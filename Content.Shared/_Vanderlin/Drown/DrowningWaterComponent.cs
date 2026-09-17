// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Sound;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Vanderlin.Drown;

/// <summary>
/// Marks a liquid as water that drowns prone entities.
/// Entities with <see cref="Stunnable.KnockedDownComponent"/> that touch this
/// (and are still alive) take <see cref="Damage"/> every <see cref="TickRate"/>
/// and play a drowning sound picked by sex.
/// </summary>
[RegisterComponent]
public sealed partial class DrowningWaterComponent : Component
{
    /// <summary>
    /// Damage applied each tick to drowning entities.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 2 },
        },
    };

    /// <summary>
    /// How often damage is applied.
    /// </summary>
    [DataField]
    public TimeSpan TickRate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Base delay between drowning sounds.
    /// </summary>
    [DataField]
    public TimeSpan SoundInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Random added/removed seconds for the sound delay.
    /// </summary>
    [DataField]
    public float SoundVariance = 1.5f;

    [DataField]
    public ProtoId<SoundCollectionPrototype> MaleSounds = "VanderlinDrownMale";

    [DataField]
    public ProtoId<SoundCollectionPrototype> FemaleSounds = "VanderlinDrownFemale";
}
