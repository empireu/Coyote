using System.Numerics;

namespace Coyote.Mathematics;

// We could have also used System.Numerics but I want doubles everywhere.
// Also, Vector2 would conflict with System.Numerics, and Vector2D would not play well with Vector2DDual.
// Translation2 doesn't look particularly enticing (even though we'd probably end up with some Rotation2d class)

public readonly struct Vector2d
{
    public static readonly Vector2d Zero = new(0, 0);
    public static readonly Vector2d One = new(1, 1);
    public static readonly Vector2d UnitX = new(1, 0);
    public static readonly Vector2d UnitY = new(0, 1);

    public double X { get; }
    public double Y { get; }

    public Vector2d(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Vector2d(double value) : this(value, value)
    {

    }

    // We may consider using functions here. Normalized() is already using it.
    public double LengthSqr => X * X + Y * Y;
    public double Length => Math.Sqrt(LengthSqr);
    public Vector2d Normalized() => this / Length;

    public override bool Equals(object? obj)
    {
        if (obj is not Vector2d other)
        {
            return false;
        }

        return Equals(other);
    }
    
    public bool Equals(Vector2d other)
    {
        return X.Equals(other.X) &&
               Y.Equals(other.Y);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"X={X}, Y={Y}";
    }

    public static bool operator ==(Vector2d a, Vector2d b) => a.Equals(b);
    public static bool operator !=(Vector2d a, Vector2d b) => !a.Equals(b);
    public static Vector2d operator +(Vector2d v) => new(+v.X, +v.Y);
    public static Vector2d operator -(Vector2d v) => new(-v.X, -v.Y);
    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator *(Vector2d a, Vector2d b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2d operator /(Vector2d a, Vector2d b) => new(a.X / b.X, a.Y / b.Y);
    public static Vector2d operator *(Vector2d v, double scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2d operator /(Vector2d v, double scalar) => new(v.X / scalar, v.Y / scalar);

    public static implicit operator Vector2(Vector2d v) => new((float)v.X, (float)v.Y);
    public static implicit operator Vector2d(Vector2 v) => new(v.X, v.Y);
}

public sealed class Vector2dDual
{
    public Dual X { get; }
    public Dual Y { get; }

    public Vector2dDual(Dual x, Dual y)
    {
        if (x.Size != y.Size)
        {
            throw new ArgumentException("X and Y duals are not of the same size");
        }

        if (x.Size == 0)
        {
            throw new ArgumentException($"Cannot create {nameof(Vector2dDual)} with empty components.");
        }

        X = x;
        Y = y;
    }

    public Vector2dDual(Dual value) : this(value, value)
    {

    }

    public int Size => X.Size;
    public bool IsReal => Size == 1;

    // We may consider using functions here. Normalized() is already using it.
    public Dual LengthSqr => X * X + Y * Y;
    public Dual Length => Dual.Sqrt(LengthSqr);
    public Vector2dDual Normalized() => this / Length;
    public Vector2d Value => new(X.Value, Y.Value);
    public Vector2dDual Head(int n = 1) => new(X.Head(n), Y.Head(n));
    public Vector2dDual Tail(int n = 1) => new(X.Tail(n), Y.Tail(n));

    public override bool Equals(object? obj)
    {
        if (obj is not Vector2dDual other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Vector2dDual other)
    {
        return X.Equals(other.X) &&
               Y.Equals(other.Y);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"X={X}, Y={Y}";
    }

    public static bool operator ==(Vector2dDual a, Vector2dDual b) => a.Equals(b);
    public static bool operator !=(Vector2dDual a, Vector2dDual b) => !a.Equals(b);
    public static Vector2dDual operator +(Vector2dDual v) => new(+v.X, +v.Y);
    public static Vector2dDual operator -(Vector2dDual v) => new(-v.X, -v.Y);
    public static Vector2dDual operator +(Vector2dDual a, Vector2dDual b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2dDual operator -(Vector2dDual a, Vector2dDual b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2dDual operator *(Vector2dDual a, Vector2dDual b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2dDual operator /(Vector2dDual a, Vector2dDual b) => new(a.X / b.X, a.Y / b.Y);
    public static Vector2dDual operator *(Vector2dDual v, Dual scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2dDual operator /(Vector2dDual v, Dual scalar) => new(v.X / scalar, v.Y / scalar);
    public static Vector2dDual operator *(Vector2dDual v, double constant) => new(v.X * constant, v.Y * constant);
    public static Vector2dDual operator /(Vector2dDual v, double constant) => new(v.X / constant, v.Y / constant);

    public static Vector2dDual Const(double x, double y, int n = 1) => new(Dual.Const(x, n), Dual.Const(y, n));
    public static Vector2dDual Var(double x, double y, int n = 1) => new(Dual.Var(x, n), Dual.Var(y, n));
}