// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Vanderlin.Glowshroom;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Vanderlin.Glowshroom;

public sealed class GlowshroomSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GlowshroomComponent, StepTriggerAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<GlowshroomComponent, StepTriggeredOnEvent>(OnStepped);
    }

    private void OnAttempt(EntityUid uid, GlowshroomComponent comp, ref StepTriggerAttemptEvent args)
    {
        // Without this the step trigger refuses to fire at all.
        args.Continue = true;
    }

    private void OnStepped(EntityUid uid, GlowshroomComponent comp, ref StepTriggeredOnEvent args)
    {
        var victim = args.Tripper;

        // Immune creatures (e.g. the alligator) don't even discharge the plant.
        if (_tag.HasTag(victim, "GlowshroomImmune"))
            return;

        var now = _timing.CurTime;
        if (now < comp.NextZap)
            return;

        comp.NextZap = now + comp.Cooldown;
        Dirty(uid, comp);

        if (!_stun.TryUpdateParalyzeDuration(victim, comp.StunTime))
            return;

        _audio.PlayPvs(
            new SoundCollectionSpecifier(comp.StepSounds, AudioParams.Default.WithVariation(0.15f)),
            victim);
        _popup.PopupEntity("Грибосвіт б'є вас розрядом!", victim, victim);
    }
}
