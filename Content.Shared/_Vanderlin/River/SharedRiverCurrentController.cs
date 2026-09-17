// SPDX-License-Identifier: AGPL-3.0-or-later
// Reference: Content.Shared/Physics/Controllers/SharedConveyorController.cs
// Same push logic as conveyors, but driven by RiverCurrentComponent which needs no power.

using System.Numerics;
using Content.Shared._Vanderlin.River;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Threading;

namespace Content.Shared._Vanderlin.River;

public abstract class SharedRiverCurrentController : VirtualController
{
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly CollisionWakeSystem _wake = default!;
    [Dependency] protected readonly EntityLookupSystem Lookup = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;

    protected const string RiverFixture = "rivercurrent";

    private RiverJob _job;

    private EntityQuery<RiverCurrentComponent> _currentQuery;
    private EntityQuery<RiverConveyedComponent> _conveyedQuery;
    private EntityQuery<RiverCurrentImmuneComponent> _immuneQuery;
    protected EntityQuery<PhysicsComponent> PhysicsQuery;
    protected EntityQuery<TransformComponent> XformQuery;

    protected HashSet<EntityUid> Intersecting = new();

    public override void Initialize()
    {
        _job = new RiverJob(this);
        _currentQuery = GetEntityQuery<RiverCurrentComponent>();
        _conveyedQuery = GetEntityQuery<RiverConveyedComponent>();
        _immuneQuery = GetEntityQuery<RiverCurrentImmuneComponent>();
        PhysicsQuery = GetEntityQuery<PhysicsComponent>();
        XformQuery = GetEntityQuery<TransformComponent>();

        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<RiverConveyedComponent, TileFrictionEvent>(OnConveyedFriction);
        SubscribeLocalEvent<RiverConveyedComponent, ComponentStartup>(OnConveyedStartup);
        SubscribeLocalEvent<RiverConveyedComponent, ComponentShutdown>(OnConveyedShutdown);

        SubscribeLocalEvent<RiverCurrentComponent, StartCollideEvent>(OnCurrentStartCollide);
        SubscribeLocalEvent<RiverCurrentComponent, ComponentStartup>(OnCurrentStartup);

        base.Initialize();
    }

    private void OnConveyedFriction(Entity<RiverConveyedComponent> ent, ref TileFrictionEvent args)
    {
        if (!ent.Comp.Conveying)
            return;

        // Same as conveyors: conveyed entities get wishdir applied instead of friction.
        args.Modifier = 0f;
    }

    private void OnConveyedStartup(Entity<RiverConveyedComponent> ent, ref ComponentStartup args)
    {
        // We need waking / sleeping to work and don't want collisionwake interfering with us.
        _wake.SetEnabled(ent.Owner, false);
    }

    private void OnConveyedShutdown(Entity<RiverConveyedComponent> ent, ref ComponentShutdown args)
    {
        _wake.SetEnabled(ent.Owner, true);
    }

    private void OnCurrentStartup(Entity<RiverCurrentComponent> ent, ref ComponentStartup args)
    {
        AwakenCurrent(ent.Owner);
    }

    /// <summary>
    /// Forcefully awakens all entities near the current.
    /// </summary>
    protected virtual void AwakenCurrent(Entity<TransformComponent?> ent)
    {
    }

    /// <summary>
    /// Wakes all river-conveyed entities contacting this current.
    /// </summary>
    protected void WakeConveyed(EntityUid currentUid)
    {
        var contacts = PhysicsSystem.GetContacts(currentUid);

        while (contacts.MoveNext(out var contact))
        {
            var other = contact.OtherEnt(currentUid);

            if (_immuneQuery.HasComp(other))
                continue;

            if (contact.OtherFixture(currentUid).Item2.Hard && contact.OtherBody(currentUid).BodyType != BodyType.Static)
            {
                EnsureComp<RiverConveyedComponent>(other);
            }

            if (_conveyedQuery.HasComp(other))
            {
                PhysicsSystem.WakeBody(other);
            }
        }
    }

    private void OnCurrentStartCollide(Entity<RiverCurrentComponent> current, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (!args.OtherFixture.Hard || args.OtherBody.BodyType == BodyType.Static)
            return;

        if (_immuneQuery.HasComp(otherUid))
            return;

        EnsureComp<RiverConveyedComponent>(otherUid);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _job.Prediction = prediction;
        _job.Conveyed.Clear();

        var query = EntityQueryEnumerator<RiverConveyedComponent, FixturesComponent, PhysicsComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var fixtures, out var physics, out var xform))
        {
            // Immune entities (e.g. fishing lures) stay where they were cast.
            if (_immuneQuery.HasComp(uid))
            {
                RemCompDeferred<RiverConveyedComponent>(uid);
                continue;
            }

            _job.Conveyed.Add(((uid, comp, fixtures, physics, xform), Vector2.Zero, false));
        }

        _parallel.ProcessNow(_job, _job.Conveyed.Count);

        foreach (var ent in _job.Conveyed)
        {
            if (!ent.Entity.Comp3.Predict && prediction)
                continue;

            var physics = ent.Entity.Comp3;

            if (physics.BodyStatus != BodyStatus.OnGround)
            {
                SetConveying(ent.Entity.Owner, ent.Entity.Comp1, false);
                continue;
            }

            var velocity = physics.LinearVelocity;
            var angularVelocity = physics.AngularVelocity;
            var targetDir = ent.Direction;

            // If mob is moving with the current then combine the directions.
            var wishDir = _mover.GetWishDir(ent.Entity.Owner);

            if (Vector2.Dot(wishDir, targetDir) > 0f)
            {
                targetDir += wishDir;
            }

            if (ent.Result)
            {
                SetConveying(ent.Entity.Owner, ent.Entity.Comp1, targetDir.LengthSquared() > 0f);

                // Same friction handling as conveyors: items get a bit of friction so they
                // settle on the centerline instead of overspeeding, mobs use their own.
                if (!_mover.UsedMobMovement.TryGetValue(ent.Entity.Owner, out var usedMob) || !usedMob)
                {
                    _mover.Friction(0.2f, frameTime: frameTime, friction: 5f, ref velocity);
                    _mover.Friction(0f, frameTime: frameTime, friction: 5f, ref angularVelocity);
                }

                SharedMoverController.Accelerate(ref velocity, targetDir, 20f, frameTime);
            }
            else if (!_mover.UsedMobMovement.TryGetValue(ent.Entity.Owner, out var usedMob) || !usedMob)
            {
                _mover.Friction(0f, frameTime: frameTime, friction: 40f, ref velocity);
                _mover.Friction(0f, frameTime: frameTime, friction: 40f, ref angularVelocity);
            }

            PhysicsSystem.SetAngularVelocity(ent.Entity.Owner, angularVelocity);
            PhysicsSystem.SetLinearVelocity(ent.Entity.Owner, velocity, wakeBody: false);

            if (!IsConveyed((ent.Entity.Owner, ent.Entity.Comp2)))
            {
                RemComp<RiverConveyedComponent>(ent.Entity.Owner);
            }
        }
    }

    private void SetConveying(EntityUid uid, RiverConveyedComponent conveyed, bool value)
    {
        if (conveyed.Conveying == value)
            return;

        conveyed.Conveying = value;
        Dirty(uid, conveyed);
    }

    /// <summary>
    /// Gets the current push direction for an entity.
    /// </summary>
    /// <returns>False if we should no longer be considered actively pushed.</returns>
    private bool TryConvey(Entity<RiverConveyedComponent, FixturesComponent, PhysicsComponent, TransformComponent> entity,
        bool prediction,
        out Vector2 direction)
    {
        direction = Vector2.Zero;
        var fixtures = entity.Comp2;
        var physics = entity.Comp3;
        var xform = entity.Comp4;

        if (!physics.Awake)
            return true;

        // Client moment
        if (!physics.Predict && prediction)
            return true;

        if (xform.GridUid == null)
            return true;

        if (physics.BodyStatus == BodyStatus.InAir ||
            _gravity.IsWeightless(entity.Owner))
        {
            return true;
        }

        Entity<RiverCurrentComponent> bestCurrent = default;
        var bestSpeed = 0f;
        var contacts = PhysicsSystem.GetContacts((entity.Owner, fixtures));
        var transform = PhysicsSystem.GetPhysicsTransform(entity.Owner);
        var anyCurrents = false;

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching)
                continue;

            var other = contact.OtherEnt(entity.Owner);

            if (!_currentQuery.TryComp(other, out var current))
                continue;

            anyCurrents = true;
            var otherFixture = contact.OtherFixture(entity.Owner);
            var otherTransform = PhysicsSystem.GetPhysicsTransform(other);

            // Check if our center is over the current, otherwise ignore it.
            if (!_fixtures.TestPoint(otherFixture.Item2.Shape, otherTransform, transform.Position))
                continue;

            if (current.Enabled && current.Speed > bestSpeed)
            {
                bestSpeed = current.Speed;
                bestCurrent = (other, current);
            }
        }

        // If we have no touching contacts we shouldn't be using conveyed anyway so nuke it.
        if (!anyCurrents)
            return true;

        if (bestSpeed == 0f || bestCurrent == default)
            return true;

        var currentXform = XformQuery.GetComponent(bestCurrent.Owner);
        var (_, currentRot) = TransformSystem.GetWorldPositionRotation(currentXform);

        currentRot += bestCurrent.Comp!.Angle;

        // No centerline steering: pure push along the flow so mobs can move freely.
        // pushDir stays unit-length so the wall check below doesn't scale with speed.
        var pushDir = currentRot.ToWorldVec();
        direction = pushDir * bestSpeed;

        // Do a final check for hard contacts so if we're pushing into a wall then NOOP.
        contacts = PhysicsSystem.GetContacts((entity.Owner, fixtures));

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.Hard || !contact.IsTouching)
                continue;

            var other = contact.OtherEnt(entity.Owner);
            var otherBody = contact.OtherBody(entity.Owner);

            // If the blocking body is dynamic then don't ignore it for this.
            if (otherBody.BodyType != BodyType.Static)
                continue;

            var otherTransform = PhysicsSystem.GetPhysicsTransform(other);
            var dotProduct = Vector2.Dot(otherTransform.Position - transform.Position, pushDir);

            if (dotProduct > 1.5f)
            {
                direction = Vector2.Zero;
                return false;
            }
        }

        return true;
    }

    public bool CanRun(RiverCurrentComponent component)
    {
        return component.Enabled && component.Speed > 0f;
    }

    private record struct RiverJob : IParallelRobustJob
    {
        public int BatchSize => 16;

        public List<(Entity<RiverConveyedComponent, FixturesComponent, PhysicsComponent, TransformComponent> Entity, Vector2 Direction, bool Result)> Conveyed = new();

        public SharedRiverCurrentController System;

        public bool Prediction;

        public RiverJob(SharedRiverCurrentController controller)
        {
            System = controller;
        }

        public void Execute(int index)
        {
            var convey = Conveyed[index];

            var result = System.TryConvey(
                (convey.Entity.Owner, convey.Entity.Comp1, convey.Entity.Comp2, convey.Entity.Comp3, convey.Entity.Comp4),
                Prediction, out var direction);

            Conveyed[index] = (convey.Entity, direction, result);
        }
    }

    /// <summary>
    /// Checks an entity's contacts to see if it's still in a current.
    /// </summary>
    private bool IsConveyed(Entity<FixturesComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        var contacts = PhysicsSystem.GetContacts(ent.Owner);

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching)
                continue;

            var other = contact.OtherEnt(ent.Owner);

            if (_currentQuery.TryComp(other, out var comp) && CanRun(comp))
                return true;
        }

        return false;
    }
}
