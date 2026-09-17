// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NPC.HTN;
using Content.Shared._Vanderlin.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Sound;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Vanderlin.Mobs;

public sealed class MobVoiceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly TimeSpan PollRate = TimeSpan.FromSeconds(0.5);
    private TimeSpan _accum = TimeSpan.Zero;

    private EntityQuery<HTNComponent> _htnQuery;

    public override void Initialize()
    {
        base.Initialize();

        _htnQuery = GetEntityQuery<HTNComponent>();

        SubscribeLocalEvent<MobVoiceComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<MobVoiceComponent, MobStateChangedEvent>(OnMobState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accum += TimeSpan.FromSeconds(frameTime);
        if (_accum < PollRate)
            return;

        _accum = TimeSpan.Zero;
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<MobVoiceComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var voice, out var htn))
        {
            EntityUid? target = null;
            if (htn.Blackboard.TryGetValue<EntityUid>("Target", out var t, EntityManager) && !Deleted(t))
                target = t;

            // New target acquired: aggro bark.
            if (target != null && target != voice.LastTarget && now >= voice.NextAggro)
            {
                Play(uid, voice.AggroSounds);
                voice.NextAggro = now + voice.AggroCooldown;
                voice.NextIdle = now + NextIdleDelay(voice);
                Dirty(uid, voice);
            }

            voice.LastTarget = target;

            // Idle noises only while peaceful.
            if (target == null && now >= voice.NextIdle)
            {
                Play(uid, voice.IdleSounds);
                voice.NextIdle = now + NextIdleDelay(voice);
                Dirty(uid, voice);
            }
        }
    }

    private void OnDamaged(EntityUid uid, MobVoiceComponent voice, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        var now = _timing.CurTime;
        if (now < voice.NextPain)
            return;

        Play(uid, voice.PainSounds);
        voice.NextPain = now + voice.PainCooldown;
        Dirty(uid, voice);
    }

    private void OnMobState(EntityUid uid, MobVoiceComponent voice, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Play(uid, voice.DeathSounds);
    }

    private void Play(EntityUid uid, ProtoId<SoundCollectionPrototype> collection)
    {
        _audio.PlayPvs(
            new SoundCollectionSpecifier(collection, AudioParams.Default.WithVariation(0.15f)),
            uid);
    }

    private TimeSpan NextIdleDelay(MobVoiceComponent voice)
    {
        var seconds = _random.NextFloat(
            (float) voice.IdleMin.TotalSeconds,
            (float) voice.IdleMax.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
