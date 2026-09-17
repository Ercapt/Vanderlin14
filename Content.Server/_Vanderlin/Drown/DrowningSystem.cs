// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Vanderlin.Drown;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Vanderlin.Drown;

public sealed class DrowningSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private TimeSpan _accum = TimeSpan.Zero;
    private static readonly TimeSpan ScanRate = TimeSpan.FromSeconds(0.5);

    private EntityQuery<DrowningWaterComponent> _waterQuery;
    private EntityQuery<KnockedDownComponent> _knockedQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;

    public override void Initialize()
    {
        base.Initialize();

        _waterQuery = GetEntityQuery<DrowningWaterComponent>();
        _knockedQuery = GetEntityQuery<KnockedDownComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidAppearanceComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accum += TimeSpan.FromSeconds(frameTime);
        if (_accum < ScanRate)
            return;

        _accum = TimeSpan.Zero;
        var now = _timing.CurTime;

        // New victims: prone entities touching drowning water, lying face-down.
        var entrants = EntityQueryEnumerator<KnockedDownComponent, PhysicsComponent>();
        while (entrants.MoveNext(out var uid, out _, out _))
        {
            if (HasComp<DrowningComponent>(uid) || _mobState.IsDead(uid) || !IsFaceDown(uid))
                continue;

            var water = GetWater(uid);
            if (water == null)
                continue;

            var drowning = EnsureComp<DrowningComponent>(uid);
            drowning.NextDamage = now;
            drowning.NextSound = now;
        }

        // Active victims: damage + sounds, or release when conditions stop holding.
        var victims = EntityQueryEnumerator<DrowningComponent>();
        while (victims.MoveNext(out var uid, out var drowning))
        {
            var water = GetWater(uid);
            if (water == null || !_knockedQuery.HasComp(uid) || _mobState.IsDead(uid) || !IsFaceDown(uid))
            {
                RemCompDeferred<DrowningComponent>(uid);
                continue;
            }

            if (now >= drowning.NextDamage)
            {
                _damageable.TryChangeDamage(uid, water.Value.Comp.Damage, true, false, origin: water.Value.Owner);
                drowning.NextDamage = now + water.Value.Comp.TickRate;
                Dirty(uid, drowning);
            }

            if (now >= drowning.NextSound)
            {
                PlaySound(uid, water.Value.Comp);
                var delay = water.Value.Comp.SoundInterval.TotalSeconds
                    + _random.NextFloat(-water.Value.Comp.SoundVariance, water.Value.Comp.SoundVariance);
                drowning.NextSound = now + TimeSpan.FromSeconds(Math.Max(0.5, delay));
                Dirty(uid, drowning);
            }
        }
    }

    /// <summary>
    /// True when the entity is lying in a drowning orientation.
    /// Everything counts except facing south.
    /// </summary>
    private bool IsFaceDown(EntityUid uid)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return false;

        return xform.WorldRotation.GetCardinalDir() != Direction.South;
    }

    /// <summary>
    /// Returns the drowning water this entity is touching, if any.
    /// </summary>
    private Entity<DrowningWaterComponent>? GetWater(EntityUid uid)
    {
        if (!_physicsQuery.TryGetComponent(uid, out _))
            return null;

        var contacts = _physics.GetContacts(uid);
        while (contacts.MoveNext(out var contact))
        {
            var other = contact.OtherEnt(uid);
            if (_waterQuery.TryComp(other, out var water))
                return (other, water);
        }

        return null;
    }

    private void PlaySound(EntityUid uid, DrowningWaterComponent water)
    {
        var collection = water.MaleSounds;
        if (_humanoidQuery.TryGetComponent(uid, out var humanoid) && humanoid.Sex == Sex.Female)
            collection = water.FemaleSounds;

        _audio.PlayPvs(
            new SoundCollectionSpecifier(collection, AudioParams.Default.WithVariation(0.15f)),
            uid);
    }
}
