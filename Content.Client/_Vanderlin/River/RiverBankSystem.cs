// SPDX-License-Identifier: AGPL-3.0-or-later
// Shows bank sprites on the sides of a river tile that have no river neighbour.
// Unlike SmoothEdge, the art stays inside the river tile itself (no +-1 tile offset).
// Bank layers follow the entity rotation, so the tile stays rotatable.

using System.Numerics;
using System.Numerics;
using Content.Shared._Vanderlin.River;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Events;

namespace Content.Client._Vanderlin.River;

public sealed class RiverBankSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Queue<EntityUid> _dirty = new();
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Pos)> _lastPos = new();

    private int _generation;

    private EntityQuery<RiverCurrentComponent> _riverQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _riverQuery = GetEntityQuery<RiverCurrentComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<RiverCurrentComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RiverCurrentComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RiverCurrentComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<RiverCurrentComponent, MoveEvent>(OnMoved);
    }

    private void OnStartup(EntityUid uid, RiverCurrentComponent comp, ComponentStartup args)
    {
        DirtyAround(uid);
    }

    private void OnShutdown(EntityUid uid, RiverCurrentComponent comp, ComponentShutdown args)
    {
        // The transform may already be detached, so refresh neighbours
        // around the last known position instead.
        if (_lastPos.TryGetValue(uid, out var last))
        {
            DirtyNeighbours(last.Grid, last.Pos);
            _lastPos.Remove(uid);
        }
    }

    private void OnAnchorChanged(EntityUid uid, RiverCurrentComponent comp, ref AnchorStateChangedEvent args)
    {
        if (args.Detaching)
        {
            HideAll(uid);
            if (_lastPos.TryGetValue(uid, out var last))
                DirtyNeighbours(last.Grid, last.Pos);
        }
        else
        {
            DirtyAround(uid);
        }
    }

    private void OnMoved(EntityUid uid, RiverCurrentComponent comp, ref MoveEvent args)
    {
        // Rotation changes which bank layer faces which world side.
        if (!args.OldRotation.EqualsApprox(args.NewRotation))
            _dirty.Enqueue(uid);
    }

    private void DirtyAround(EntityUid uid)
    {
        _dirty.Enqueue(uid);

        if (!_xformQuery.TryGetComponent(uid, out var xform) || !xform.Anchored)
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var gridUid = xform.GridUid.Value;
        var pos = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        DirtyNeighbours(gridUid, pos);
    }

    private void DirtyNeighbours(EntityUid gridUid, Vector2i pos)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        DirtyAt(gridUid, grid, pos + new Vector2i(1, 0));
        DirtyAt(gridUid, grid, pos + new Vector2i(-1, 0));
        DirtyAt(gridUid, grid, pos + new Vector2i(0, 1));
        DirtyAt(gridUid, grid, pos + new Vector2i(0, -1));
    }

    private void DirtyAt(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, pos);
        while (enumerator.MoveNext(out var ent))
        {
            if (_riverQuery.HasComp(ent.Value))
                _dirty.Enqueue(ent.Value);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_dirty.Count == 0)
            return;

        _generation++;

        var seen = new HashSet<EntityUid>();

        while (_dirty.TryDequeue(out var uid))
        {
            if (!seen.Add(uid))
                continue;

            UpdateBanks(uid);
        }
    }

    private void UpdateBanks(EntityUid uid)
    {
        if (!_riverQuery.TryGetComponent(uid, out _) || !_spriteQuery.TryGetComponent(uid, out var sprite))
            return;

        if (!_xformQuery.TryGetComponent(uid, out var xform)
            || !xform.Anchored
            || xform.GridUid == null
            || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            HideAll(uid);
            return;
        }

        var gridUid = xform.GridUid.Value;
        var pos = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        _lastPos[uid] = (gridUid, pos);

        var noRiverNorth = !HasRiver(gridUid, grid, pos.Offset(Direction.North));
        var noRiverSouth = !HasRiver(gridUid, grid, pos.Offset(Direction.South));
        var noRiverEast = !HasRiver(gridUid, grid, pos.Offset(Direction.East));
        var noRiverWest = !HasRiver(gridUid, grid, pos.Offset(Direction.West));

        // Bank art rotates with the entity, so figure out which world side
        // each layer currently faces and drive it from that side's neighbour.
        var rot = xform.LocalRotation;
        var ent = (uid, sprite);
        foreach (var (layer, artDir) in BankLayers)
        {
            var facing = Angle.FromWorldVec(rot.RotateVec(artDir)).GetDir();
            var show = facing switch
            {
                Direction.North => noRiverNorth,
                Direction.South => noRiverSouth,
                Direction.East => noRiverEast,
                Direction.West => noRiverWest,
                _ => false,
            };
            _sprite.LayerSetVisible(ent, layer, show);
        }
    }

    private bool HasRiver(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, pos);
        while (enumerator.MoveNext(out var ent))
        {
            if (_riverQuery.HasComp(ent.Value))
                return true;
        }

        return false;
    }

    private void HideAll(EntityUid uid)
    {
        if (!_spriteQuery.TryGetComponent(uid, out var sprite))
            return;

        var ent = (uid, sprite);
        _sprite.LayerSetVisible(ent, RiverBankLayer.North, false);
        _sprite.LayerSetVisible(ent, RiverBankLayer.South, false);
        _sprite.LayerSetVisible(ent, RiverBankLayer.East, false);
        _sprite.LayerSetVisible(ent, RiverBankLayer.West, false);
    }

    /// <summary>
    /// Bank layer with the local direction its art is drawn for.
    /// </summary>
    private static readonly (RiverBankLayer Layer, Vector2 ArtDir)[] BankLayers =
    [
        (RiverBankLayer.South, new Vector2(0, -1)),
        (RiverBankLayer.East, new Vector2(1, 0)),
        (RiverBankLayer.North, new Vector2(0, 1)),
        (RiverBankLayer.West, new Vector2(-1, 0)),
    ];
}
