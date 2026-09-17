// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Vanderlin.Leech;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Vanderlin.Leech;

public sealed class LeechSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private static readonly EntProtoId LeechProto = "Leech";
    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SuckRate = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeechDuration = TimeSpan.FromMinutes(10);

    private static readonly DamageSpecifier BiteDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 10 },
        },
    };

    private static readonly DamageSpecifier PoisonHeal = new()
    {
        DamageDict = new()
        {
            { "Poison", -0.5 },
        },
    };

    private TimeSpan _checkAccum = TimeSpan.Zero;
    private TimeSpan _suckAccum = TimeSpan.Zero;

    private EntityQuery<SwampWaterComponent> _swampQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<BloodstreamComponent> _bloodQuery;

    public override void Initialize()
    {
        base.Initialize();

        _swampQuery = GetEntityQuery<SwampWaterComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _bloodQuery = GetEntityQuery<BloodstreamComponent>();

        SubscribeLocalEvent<AttachedLeechComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AttachedLeechComponent, GetVerbsEvent<UtilityVerb>>(OnGetVerbs);
        SubscribeLocalEvent<LeechComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var dt = TimeSpan.FromSeconds(frameTime);
        _checkAccum += dt;
        _suckAccum += dt;

        if (_checkAccum >= CheckRate)
        {
            _checkAccum = TimeSpan.Zero;
            CheckHosts();
        }

        if (_suckAccum >= SuckRate)
        {
            _suckAccum = TimeSpan.Zero;
            SuckBlood();
        }
    }

    private void CheckHosts()
    {
        var now = _timing.CurTime;

        // New hosts: creatures wading in swamp water without a leech yet.
        var candidates = EntityQueryEnumerator<BloodstreamComponent, PhysicsComponent>();
        while (candidates.MoveNext(out var uid, out _, out _))
        {
            if (HasComp<AttachedLeechComponent>(uid) || _mobState.IsDead(uid))
                continue;

            var swamp = GetSwamp(uid);
            if (swamp == null)
                continue;

            var chance = IsShod(uid) ? swamp.Value.Comp.ShodChance : swamp.Value.Comp.BarefootChance;
            if (!_random.Prob(chance))
                continue;

            AttachLeech(uid);
        }

        // Overfed leeches fall off by themselves.
        var hosts = EntityQueryEnumerator<AttachedLeechComponent>();
        while (hosts.MoveNext(out var uid, out var attached))
        {
            if (now >= attached.DetachAt)
                DetachLeech(uid, attached, fed: true);
        }
    }

    private void SuckBlood()
    {
        // Attached leeches: drink blood, cleanse poison. Keeps going
        // even out of water until removed or full.
        var hosts = EntityQueryEnumerator<AttachedLeechComponent>();
        while (hosts.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (_bloodQuery.TryComp(uid, out var blood))
                _bloodstream.TryModifyBloodLevel((uid, blood), FixedPoint2.New(-0.5));

            _damageable.TryChangeDamage(uid, PoisonHeal, true);
        }
    }

    private void AttachLeech(EntityUid uid)
    {
        var attached = EnsureComp<AttachedLeechComponent>(uid);
        attached.LeftLeg = _random.Next(2) == 0;
        attached.DetachAt = _timing.CurTime + LeechDuration;
        Dirty(uid, attached);

        _damageable.TryChangeDamage(uid, BiteDamage, true);
        _popup.PopupEntity("Ви відчуваєте слабкий біль на нозі.", uid, uid);
    }

    private void DetachLeech(EntityUid uid, AttachedLeechComponent attached, bool fed)
    {
        RemCompDeferred<AttachedLeechComponent>(uid);

        var leech = Spawn(LeechProto, Transform(uid).Coordinates);
        if (TryComp<LeechComponent>(leech, out var leechComp))
        {
            leechComp.Fed = fed;
            Dirty(leech, leechComp);
        }

        if (fed)
        {
            _popup.PopupEntity("Напившись крові, п'явка відпадає.", uid, uid);
            return;
        }

        if (_hands.TryPickupAnyHand(uid, leech))
            _popup.PopupEntity("Ви знімаєте п'явку.", uid, uid);
        else
            _popup.PopupEntity("Ви знімаєте п'явку, але руки зайняті — вона падає додолу.", uid, uid);
    }

    private void OnGetVerbs(EntityUid uid, AttachedLeechComponent attached, GetVerbsEvent<UtilityVerb> args)
    {
        // Only the host removes its own leech. No CanInteract check on purpose:
        // victims are usually knocked down, and pulling a leech off your own
        // leg needs no hands-free consciousness check.
        if (args.User != args.Target)
            return;

        args.Verbs.Add(new UtilityVerb
        {
            Text = "Зняти п'явку",
            Act = () => DetachLeech(uid, attached, fed: false),
        });
    }

    private void OnExamined(EntityUid uid, AttachedLeechComponent attached, ExaminedEvent args)
    {
        var leg = attached.LeftLeg ? "лівій" : "правій";
        args.PushMarkup($"На {leg} нозі висить п'явка.");
    }

    private void OnUseInHand(EntityUid uid, LeechComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        if (comp.Fed)
        {
            _popup.PopupEntity("П'явка вже напилася крові і чіплятися не хоче.", uid, user);
            return;
        }

        if (HasComp<AttachedLeechComponent>(user))
        {
            _popup.PopupEntity("На вас вже висить п'явка.", uid, user);
            return;
        }

        if (GetSwamp(user) == null)
        {
            _popup.PopupEntity("П'явка чіпляється лише у болотній воді.", uid, user);
            return;
        }

        args.Handled = true;
        QueueDel(uid);
        AttachLeech(user);
    }

    private Entity<SwampWaterComponent>? GetSwamp(EntityUid uid)
    {
        if (!_physicsQuery.TryGetComponent(uid, out _))
            return null;

        var contacts = _physics.GetContacts(uid);
        while (contacts.MoveNext(out var contact))
        {
            var other = contact.OtherEnt(uid);
            if (_swampQuery.TryComp(other, out var swamp))
                return (other, swamp);
        }

        return null;
    }

    private bool IsShod(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "shoes", out _);
    }
}
