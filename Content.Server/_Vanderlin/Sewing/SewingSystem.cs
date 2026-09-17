// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Vanderlin.Sewing;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._Vanderlin.Sewing;

public sealed class SewingSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;

    private static readonly EntProtoId NeedleProto = "ThreadedNeedle";

    private static readonly DamageSpecifier PrickDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 2 },
        },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThreadComponent, AfterInteractEvent>(OnThreadUse);
        SubscribeLocalEvent<ThreadedNeedleComponent, AfterInteractEvent>(OnNeedleUse);
        SubscribeLocalEvent<ThreadedNeedleComponent, UseInHandEvent>(OnNeedleUseInHand);
        SubscribeLocalEvent<ThreadedNeedleComponent, ExaminedEvent>(OnNeedleExamined);
    }

    private void OnThreadUse(EntityUid uid, ThreadComponent thread, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        var user = args.User;
        var target = args.Target.Value;

        // Thread + thorn = threaded needle.
        if (HasComp<ThornComponent>(target))
        {
            args.Handled = true;
            QueueDel(uid);
            QueueDel(target);

            var needle = Spawn(NeedleProto, Transform(user).Coordinates);
            if (!_hands.TryPickupAnyHand(user, needle))
                _popup.PopupEntity("Ви робите голку з ниткою, але руки зайняті — вона падає додолу.", user, user);
            else
                _popup.PopupEntity("Ви робите голку з ниткою.", user, user);

            return;
        }

        // Thread + used needle = re-threaded needle.
        if (TryComp<ThreadedNeedleComponent>(target, out var needleComp) && needleComp.Used)
        {
            args.Handled = true;
            QueueDel(uid);

            needleComp.Used = false;
            Dirty(target, needleComp);
            _appearance.SetData(target, NeedleVisuals.Used, false);
            _popup.PopupEntity("Ви знову заправляєте нитку в голку.", user, user);
        }
    }

    private void OnNeedleUse(EntityUid uid, ThreadedNeedleComponent needle, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        args.Handled = true;
        TryStitch(uid, needle, args.User, args.Target.Value);
    }

    private void OnNeedleUseInHand(EntityUid uid, ThreadedNeedleComponent needle, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryStitch(uid, needle, args.User, args.User);
    }

    private void OnNeedleExamined(EntityUid uid, ThreadedNeedleComponent needle, ExaminedEvent args)
    {
        args.PushMarkup("Нею зашивають рани: оберіть частину тіла на ляльці та натисніть нею на пораненого.");
    }

    /// <summary>
    /// Stitches bleeding wounds on the body part selected on the user's targeting doll.
    /// </summary>
    private void TryStitch(EntityUid uid, ThreadedNeedleComponent needle, EntityUid user, EntityUid target)
    {
        if (needle.Used)
        {
            _popup.PopupEntity("Голка вже використана, заправте нитку знову.", uid, user);
            return;
        }

        if (!TryComp<BodyComponent>(target, out var body)
            || !TryComp<TargetingComponent>(user, out var targeting))
        {
            return;
        }

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);
        var part = _body.GetBodyChildrenOfType(target, partType, body, symmetry).ToList().FirstOrDefault();
        if (part.Id == EntityUid.Invalid || !TryComp<WoundableComponent>(part.Id, out var woundable))
        {
            _popup.PopupEntity("Тут нема чого зашивати.", uid, user);
            return;
        }

        if (!_wounds.TryHealBleedingWounds(part.Id, -5f, out _, woundable))
        {
            _popup.PopupEntity("На цій частині тіла немає кровоточивих ран.", uid, user);
            return;
        }

        _damageable.TryChangeDamage(part.Id, PrickDamage, true, origin: user);

        needle.Used = true;
        Dirty(uid, needle);
        _appearance.SetData(uid, NeedleVisuals.Used, true);
        _popup.PopupEntity("Ви зашиваєте рану.", uid, user);
    }
}
