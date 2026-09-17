// SPDX-License-Identifier: AGPL-3.0-or-later
// Walk/run speed multipliers for Vanderlin natural tiles.
// Road is fast, mud is slow. Refreshes when the mover changes tiles.

using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Vanderlin.Tiles;

public sealed class TileSpeedSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    private static readonly Dictionary<string, float> SpeedMultipliers = new()
    {
        { "VanderlinRoad", 1.1f },
        { "VanderlinMud", 0.8f },
    };

    private readonly Dictionary<EntityUid, int> _lastTile = new();

    private EntityQuery<MovementSpeedModifierComponent> _moverQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _moverQuery = GetEntityQuery<MovementSpeedModifierComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<MovementSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<MovementSpeedModifierComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MovementSpeedModifierComponent, MoveEvent>(OnMoved);
    }

    private void OnShutdown(EntityUid uid, MovementSpeedModifierComponent comp, ComponentShutdown args)
    {
        _lastTile.Remove(uid);
    }

    private void OnMoved(EntityUid uid, MovementSpeedModifierComponent comp, ref MoveEvent args)
    {
        var tile = GetTileId(uid);
        if (tile == null)
        {
            // Left the grid: drop any stale multiplier.
            if (_lastTile.Remove(uid))
                _movement.RefreshMovementSpeedModifiers(uid);
            return;
        }

        if (_lastTile.TryGetValue(uid, out var last) && last == tile.Value)
            return;

        _lastTile[uid] = tile.Value;
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefresh(EntityUid uid, MovementSpeedModifierComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        var tile = GetTileId(uid);
        if (tile == null)
        {
            // Guarded remove: refresh only once so this can't recurse.
            if (_lastTile.Remove(uid))
                _movement.RefreshMovementSpeedModifiers(uid);
            return;
        }

        var def = (ContentTileDefinition) _tileDefs[tile.Value];
        if (SpeedMultipliers.TryGetValue(def.ID, out var mult))
            args.ModifySpeed(mult, mult);
    }

    private int? GetTileId(EntityUid uid)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform)
            || xform.GridUid == null
            || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            return null;
        }

        if (!_map.TryGetTileRef(xform.GridUid.Value, grid, xform.Coordinates, out var tileRef))
            return null;

        return tileRef.Tile.TypeId;
    }
}
