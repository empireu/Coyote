using System.Drawing;
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

    public static List<Entity> Clip(this World world, Vector2 pickPosition)
    {
        var query = world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent>());
        var results = new List<Entity>();

        foreach (var chunk in query)
        {
            foreach (var entity in chunk.Entities)
            {
                if (entity.IsAlive() && entity.GetRectangle().Contains(pickPosition.X, pickPosition.Y))
                {
                    results.Add(entity);
                }
            }
        }

        return results;
    }

    public static void Move(this Entity entity, Vector2 position)
    {
        var world = entity.GetWorld();

        ref var component = ref world.Get<PositionComponent>(entity);
        
        var oldPosition = component.Position;

        if (oldPosition == position)
        {
            return;
        }

        component.Position = position;

        component.UpdateCallback?.Invoke(entity, oldPosition);
    }

    public static Vector2 Xy(this Vector3 v)
    {
        return new Vector2(v.X, v.Y);
    }

    public static bool ApproxEquals(this float f, float other, float threshold = 10e-6f)
    {
        return Math.Abs(f - other) < threshold;
    }

    public static bool ApproxEquals(this double d, double other, double threshold = 10e-6)
    {
        return Math.Abs(d - other) < threshold;
    }
}