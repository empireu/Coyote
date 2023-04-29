using System.Drawing;
using System.Numerics;
using System.Text;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.Mathematics;
using GameFramework.Extensions;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Renderer.VertexFormats;
using GameFramework.Utilities.Extensions;
using Vortice.Mathematics;

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

    public delegate bool ClipConditionDelegate(Entity entity, RectangleF entityRectangle, bool initialCheck);

    public static List<Entity> Clip(this World world, Vector2 pickPosition, AlignMode align = AlignMode.Center, SizeF? margin = null, ClipConditionDelegate? condition = null)
    {
        margin ??= SizeF.Empty;
        condition ??= (_, _, check) => check;
        var results = new List<Entity>();

        world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent>(), new ForEachWithEntity<PositionComponent, ScaleComponent>(
            (in Entity e, ref PositionComponent _, ref ScaleComponent _) =>
            {
                var r = e.GetRectangle();
               
                r.Inflate(margin.Value);

                if (!condition(e, r, align == AlignMode.Center
                        ? r.Contains(pickPosition.X, pickPosition.Y)
                        : r.Contains(pickPosition.X - r.Width / 2,
                            pickPosition.Y + r.Height / 2)))
                {
                    return;
                }

                if (e.Has<SpriteComponent>() && e.Get<SpriteComponent>().Disabled)
                {
                    return;
                }

                results.Add(e);
            }));

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

    /// <summary>
    ///     Iterates over the items.
    /// </summary>
    /// <param name="enumerable"></param>
    /// <param name="body">The function to invoke. If false is returned, iteration will end there.</param>
    /// <returns>The original list.</returns>
    public static void ForEach<TItem>(this IEnumerable<TItem> enumerable, Func<TItem, bool> body)
    {
        foreach (var item in enumerable)
        {
            if (!body(item))
            {
                break;
            }
        }
    }

    /// <summary>
    ///     Iterates over the list of items.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="body">The function to invoke.</param>
    /// <returns>The original list.</returns>
    public static void ForEach<TItem>(this IEnumerable<TItem> list, Action<TItem> body)
    {
        foreach (var item in list)
        {
            body(item);
        }
    }

    /// <summary>
    ///     Iterates over the list of items.  This calls <see cref="ForEach{TList,TItem}"/>
    ///     Afterwards, the list is cleared.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="body">The function to invoke. If false is returned, iteration will end there.</param>
    /// <returns>The original list.</returns>
    public static void RemoveAll<TItem>(this IList<TItem> list, Func<TItem, bool> body)
    {
        list.ForEach(body);
        list.Clear();
    }

    /// <summary>
    ///     Iterates over the list of items.
    ///     Afterwards, the list is cleared.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="body">The function to invoke.</param>
    /// <returns>The original list.</returns>
    public static void RemoveAll<TItem>(this IList<TItem> list, Action<TItem> body)
    {
        list.ForEach(body);
        list.Clear();
    }

    public static T Also<T>(this T obj, Action<T> action)
    {
        action(obj);
        return obj;
    }

    public static T2 Map<T1, T2>(this T1 t1, Func<T1, T2> function)
    {
        return function(t1);
    }

    /// <summary>
    ///     Reads a line from the <see cref="stream"/>, with a limit of <see cref="maxLength"/> bytes.
    /// </summary>
    public static async ValueTask<string?> ReadLineSized(this Stream stream, int maxLength, CancellationToken token = default)
    {
        await using var ms = new MemoryStream();

        var total = 0;
        var buffer = new byte[1024];

        while (total < maxLength)
        {
            int read;

            try
            {
                read = await stream.ReadAsync(buffer, token);
            }
            catch (IOException)
            {
                // Connection closed

                return null;
            }

            if (read == 0)
            {
                if (total > 0)
                {
                    break;
                }

                // Connection was closed without us receiving anything.
                return null;
            }

            total += read;

            ms.Write(buffer.AsSpan(0, read));

            var end = buffer[read - 1];

            if (end is 13 or 10)
            {
                // CR LF
                break;
            }
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static Vector2 MaxWith(this Vector2 a, Vector2 b)
    {
        return new Vector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    public static Rotation Exp(this Vector2 r2)
    {
        return Rotation.FromDirection(r2.X, r2.Y);
    }
}