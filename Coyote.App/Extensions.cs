using System.Drawing;
using System.Numerics;
using System.Text;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.App.Nodes;
using Coyote.Mathematics;
using GameFramework.Renderer;

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

    /// <summary>
    ///     Scans the world for entities that intersect the specified <see cref="pickPosition"/>.
    /// </summary>
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

    public static IReadOnlyList<TItem> ForEach<TItem>(this IReadOnlyList<TItem> list, Action<TItem> body)
    {
        list.AsEnumerable().ForEach(body);

        return list;
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

    /// <summary>
    ///     Runs the specified operation <see cref="action"/> on the object <see cref="obj"/> and returns <see cref="obj"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T Also<T>(this T obj, Action<T> action)
    {
        action(obj);
        return obj;
    }

    /// <summary>
    ///     Maps the value <see cref="t1"/> to <see cref="T2"/> using the specified transform <see cref="function"/> and returns the result.
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <param name="t1"></param>
    /// <param name="function"></param>
    /// <returns></returns>
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

    /// <summary>
    ///     Gets an array with all the elements in <see cref="e"/>.
    /// </summary>
    public static T[] Bind<T>(this IEnumerable<T> e) => e.ToArray();

    /// <summary>
    ///     Gets an array with the first <see cref="n"/> elements in <see cref="e"/>. It will return an empty array if <see cref="e"/> has no elements.
    /// </summary>
    public static T[] TakeBind<T>(this IEnumerable<T> e, int n = 1) => e.Take(n).Bind();

    /// <summary>
    ///     Gets the first element in <see cref="e"/> or null, if <see cref="e"/> has no elements.
    /// </summary>
    public static T? BindFirst<T>(this IEnumerable<T> e) => e.FirstOrDefault();

    /// <summary>
    ///     Executes the operation <see cref="action"/> on the first element in <see cref="e"/>, if <see cref="e"/> is not empty.
    /// </summary>
    public static void IfPresent<T>(this IEnumerable<T> e, Action<T> action)
    {
        var elements = e.TakeBind();

        if (elements.Length != 0)
        {
            action(elements[0]);
        }
    }

    /// <summary>
    ///     Short-hand for fetching the <see cref="PositionComponent"/>'s position.
    /// </summary>
    public static Vector2 Position(this Entity entity) => entity.Get<PositionComponent>().Position;

    /// <summary>
    ///     Short-hand for getting a reference to the <see cref="PositionComponent"/>'s position.
    /// </summary>
    public static ref Vector2 PositionRef(in this Entity entity) => ref entity.Get<PositionComponent>().Position;

    /// <summary>
    ///     Short-hand for fetching the <see cref="ScaleComponent"/>'s scale.
    /// </summary>
    public static Vector2 Scale(this Entity entity) => entity.Get<ScaleComponent>().Scale;

    /// <summary>
    ///     Short-hand for getting a reference to the <see cref="ScaleComponent"/>'s scale.
    /// </summary>
    public static ref Vector2 ScaleRef(in this Entity entity) => ref entity.Get<ScaleComponent>().Scale;

    /// <summary>
    ///     Appends the element <see cref="tail"/> to the end of the enumerable <see cref="head"/>.
    /// </summary>
    public static IEnumerable<T> Append<T>(this IEnumerable<T> head, T tail)
    {
        foreach (var t in head)
        {
            yield return t;
        }

        yield return tail;
    }

    /// <summary>
    ///     Appends the enumerable <see cref="tail"/> to the end of the enumerable <see cref="head"/>. This is equivalent to <code>head.Concat(tail)</code>
    /// </summary>
    public static IEnumerable<T> Append<T>(this IEnumerable<T> head, IEnumerable<T> tail)
    {
        foreach (var t in head)
        {
            yield return t;
        }

        foreach (var t in tail)
        {
            yield return t;
        }
    }

    /// <summary>
    ///     Pre-pends the element <see cref="head"/> to the start of the enumerable <see cref="tail"/>.
    /// </summary>
    public static IEnumerable<T> Prepend<T>(this IEnumerable<T> tail, T head)
    {
        yield return head;

        foreach (var t in tail)
        {
            yield return t;
        }
    }

    /// <summary>
    ///     Creates an enumeration with the element <see cref="head"/>.
    /// </summary>
    public static IEnumerable<T> Stream<T>(this T head) => new[] { head };
    
    /// <summary>
    ///     Searches the <see cref="World"/> using the specified <see cref="query"/> and returns all results.
    /// </summary>
    public static List<Entity> ToArray(this Query query)
    {
        var results = new List<Entity>(128);

        foreach (ref var chunk in query.GetChunkIterator())
        {
            foreach (var entity in chunk)
            {
                results.Add(chunk.Entity(entity));
            }
        }

        return results;
    }

    /// <summary>
    ///     Checks if the <see cref="World"/> contains any entities that <see cref="match"/> the query.
    /// </summary>
    public static bool Any(this Query query, Predicate<Entity> match)
    {
        foreach (ref var chunk in query.GetChunkIterator())
        {
            foreach (var id in chunk)
            {
                var entity = chunk.Entity(id);

                if (match(entity))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public delegate bool EntityConsumerDelegate(Entity entity);

    public static void Scan(this Query query, EntityConsumerDelegate consumer)
    {
        foreach (ref var chunk in query.GetChunkIterator())
        {
            foreach (var id in chunk)
            {
                var entity = chunk.Entity(id);

                if (!consumer(entity))
                {
                    return;
                }
            }
        }
    }

    public static Entity? FirstOrNull(this Query query, Predicate<Entity> match)
    {
        foreach (ref var chunk in query.GetChunkIterator())
        {
            foreach (var id in chunk)
            {
                var entity = chunk.Entity(id);

                if (match(entity))
                {
                    return entity;
                }
            }
        }

        return null;
    }

    public static Entity First(this Query query, Predicate<Entity> match)
    {
        return query.FirstOrNull(match) ?? throw new Exception("Could not find entity that matches the predicate");
    }

    public static List<NodeChild> Children(this Entity entity) => entity.Get<NodeComponent>().ChildrenRef.Instance;
    public static IEnumerable<Entity> ChildrenEnt(this Entity entity) => entity.Children().Select(x => x.Entity);
}