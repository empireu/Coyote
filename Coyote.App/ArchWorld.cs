using System.Collections.Concurrent;
using Arch.Core;
using GameFramework.Utilities;

namespace Coyote.App;

internal static class ArchWorld
{
    // I found some issues with Arch's design. Destroying a world causes issues with invalid entity world IDs.
    // For now, I am just pooling the worlds.
    // Also, world creation is not thread-safe, apparently.

    private static readonly Stack<World> Worlds = new();
    private static readonly HashSet<World> InUse = new();

    public static World Get() =>
        (Worlds.Count > 0 ? Worlds.Pop() : World.Create()).Also(w => Assert.IsTrue(InUse.Add(w)));

    public static void Return(World world)
    {
        Assert.IsTrue(InUse.Remove(world));
        world.Clear();
        Worlds.Push(world);
    }
}