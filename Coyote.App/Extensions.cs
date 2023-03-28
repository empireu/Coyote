﻿using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;

namespace Coyote.App;

internal static class Extensions
{
    public static void EnsureAlive(this Entity entity)
    {
        if (!entity.IsAlive())
        {
            throw new InvalidOperationException("Tried to perform operation on dead entity.");
        }
    }

    public static World GetWorld(this Entity entity)
    {
        return World.Worlds[entity.WorldId];
    }

    public static RectangleF GetRectangle(this Entity entity)
    {
        entity.EnsureAlive();

        var world = entity.GetWorld();
        var position = world.Get<PositionComponent>(entity).Position;
        var scale = world.Get<ScaleComponent>(entity).Scale;

        return new RectangleF(position.X - scale.X / 2, position.Y - scale.Y / 2, scale.X, scale.Y);
    }

    public static Entity? PickEntity(this World world, Vector2 pickPosition)
    {
        var query = world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent>());

        foreach (var chunk in query)
        {
            foreach (var entity in chunk.Entities)
            {
                if (entity.GetRectangle().Contains(pickPosition.X, pickPosition.Y))
                {
                    return entity;
                }
            }
        }

        return null;
    }

    public static void Move(this Entity entity, Vector2 position)
    {
        var world = entity.GetWorld();

        ref var component = ref world.Get<PositionComponent>(entity);

        component.Position = position;
        component.UpdateCallback?.Invoke();
    }
}