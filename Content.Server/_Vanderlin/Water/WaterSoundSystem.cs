// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Vanderlin.Drown;
using Content.Shared.Sound;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Vanderlin.Water;

public sealed class WaterSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly ProtoId<SoundCollectionPrototype> LandSounds = "VanderlinWaterLand";

    public override void Initialize()
    {
        base.Initialize();

        // DrowningWater StepTriggers fire StepTriggeredOffEvent on step-in (StepOn defaults to false).
        SubscribeLocalEvent<DrowningWaterComponent, StepTriggeredOffEvent>(OnEnter);
    }

    private void OnEnter(EntityUid uid, DrowningWaterComponent comp, ref StepTriggeredOffEvent args)
    {
        _audio.PlayPvs(
            new SoundCollectionSpecifier(LandSounds, AudioParams.Default.WithVariation(0.15f)),
            uid);
    }
}
