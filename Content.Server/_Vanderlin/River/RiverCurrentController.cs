// SPDX-License-Identifier: AGPL-3.0-or-later
// Reference: Content.Server/Physics/Controllers/ConveyorController.cs (fixture + wake parts only).
// No power, no signals, no visuals: the river always flows.

using Content.Shared._Vanderlin.River;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Vanderlin.River;

public sealed class RiverCurrentController : SharedRiverCurrentController
{
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RiverCurrentComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RiverCurrentComponent, ComponentShutdown>(OnShutdown);

        base.Initialize();
    }

    private void OnInit(EntityUid uid, RiverCurrentComponent component, ComponentInit args)
    {
        if (!PhysicsQuery.TryComp(uid, out var physics))
            return;

        var shape = new PolygonShape();
        shape.SetAsBox(0.55f, 0.55f);

        _fixtures.TryCreateFixture(uid, shape, RiverFixture,
            collisionLayer: (int) (CollisionGroup.LowImpassable | CollisionGroup.MidImpassable |
                                   CollisionGroup.Impassable),
            hard: false,
            body: physics);
    }

    private void OnShutdown(EntityUid uid, RiverCurrentComponent component, ComponentShutdown args)
    {
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!PhysicsQuery.TryComp(uid, out var physics))
            return;

        _fixtures.DestroyFixture(uid, RiverFixture, body: physics);
    }

    /// <summary>
    /// Awakens sleeping entities on the current's tile when it spawns.
    /// Needed so CollisionWake bodies that start the round inside the river get pushed.
    /// </summary>
    protected override void AwakenCurrent(Entity<TransformComponent?> ent)
    {
        if (!XformQuery.Resolve(ent.Owner, ref ent.Comp))
            return;

        var xform = ent.Comp;
        var tileRef = _turf.GetTileRef(xform.Coordinates);

        if (tileRef == null)
            return;

        Intersecting.Clear();
        Lookup.GetLocalEntitiesIntersecting(
            tileRef.Value.GridUid,
            tileRef.Value.GridIndices,
            Intersecting,
            0f,
            flags: LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate);

        foreach (var entity in Intersecting)
        {
            if (!PhysicsQuery.TryGetComponent(entity, out var physics))
                continue;

            if (physics.BodyType != BodyType.Static)
                PhysicsSystem.WakeBody(entity, body: physics);
        }
    }
}
