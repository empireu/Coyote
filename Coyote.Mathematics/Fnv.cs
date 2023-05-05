using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Coyote.Mathematics;

/// <summary>
///     <see href="https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function">Fowler–Noll–Vo hash function</see>
///     FNV-1a Implementation. It is a simple NC hashing algorithm that has a low collision rate.
/// </summary>
public sealed class FnvStream
{
    private const long Prime = 1099511628211;
    private const long BasisOffset = unchecked((long)14695981039346656037);

    public long Result { get; private set; } = BasisOffset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(byte b)
    {
        unchecked
        {
            Result ^= b;
            Result *= Prime;
        }
    }

    public void Add(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            Add(bytes[i]);
        }
    }

    public void Add(double d)
    {
        Span<byte> span = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(span, d);
        Add(span);
    }

    public void Add(Vector2d vector)
    {
        Add(vector.X);
        Add(vector.Y);
    }

    public void Add(Pose2d pose)
    {
        Add(pose.Translation);
        Add(pose.Rotation.Direction);
    }
}