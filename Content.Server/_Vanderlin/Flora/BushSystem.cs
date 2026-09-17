// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Vanderlin.Flora;
using Content.Shared._Vanderlin.Sewing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Sound;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Vanderlin.Flora;

public sealed class BushSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly EntProtoId ThornProto = "Thorn";
    private static readonly ProtoId<SoundCollectionPrototype> RustleSounds = "VanderlinPlantcross";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BushComponent, InteractHandEvent>(OnInteract);
    }

    private void OnInteract(EntityUid uid, BushComponent bush, InteractHandEvent args)
    {
        var user = args.User;

        if (bush.Depleted)
        {
            _popup.PopupEntity("Ви намагаєтеся знайти щось цінне, та кущ пустий.", uid, user);
            return;
        }

        _audio.PlayPvs(
            new SoundCollectionSpecifier(RustleSounds, AudioParams.Default.WithVariation(0.15f)),
            uid);

        bush.RummagesLeft--;

        if (_random.Prob(bush.ThornChance))
        {
            var thorn = Spawn(ThornProto, Transform(uid).Coordinates);
            if (!_hands.TryPickupAnyHand(user, thorn))
                _popup.PopupEntity("Ви виймаєте з куща шип, але руки зайняті — він падає додолу.", uid, user);
            else
                _popup.PopupEntity("Ви виймаєте з куща шип.", uid, user);

            Deplete(uid, bush);
            return;
        }

        if (bush.RummagesLeft <= 0)
        {
            Deplete(uid, bush);
            return;
        }

        Dirty(uid, bush);
        _popup.PopupEntity("Ви риєтеся в кущі, але нічого цінного не знаходите.", uid, user);
    }

    private void Deplete(EntityUid uid, BushComponent bush)
    {
        bush.Depleted = true;
        bush.RummagesLeft = 0;
        Dirty(uid, bush);

        _appearance.SetData(uid, BushVisuals.Depleted, true);
        _popup.PopupEntity("Ви намагаєтеся знайти щось цінне, та кущ пустий.", uid, uid);
    }
}
