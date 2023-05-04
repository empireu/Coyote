using System.Numerics;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using GameFramework.Utilities;

namespace Coyote.Mathematics;

/// <summary>
///     Represents a number interval.
/// </summary>
public readonly struct Range
{
    public static readonly Range Invalid = new(1, 0);
    public static readonly Range R = new(double.MinValue, double.MaxValue);
    public static readonly Range R0Plus = new(0, double.MaxValue);
    public static readonly Range RPlus = new(double.Epsilon, double.MaxValue);

    public double Min { get; }
    public double Max { get; }

    public bool IsValid => CheckValidity(this);

    public Range(double min, double max)
    {
        Min = min;
        Max = max;
    }

    public override string ToString()
    {
        return $"{Min}:{Max}";
    }

    public static bool CheckValidity(Range r)
    {
        return !double.IsNaN(r.Min) && !double.IsNaN(r.Max) && r.Min < r.Max;
    }

    public static Range Intersect(Range r1, Range r2)
    {
        var start = Math.Max(r1.Min, r2.Min);
        var end = Math.Min(r1.Max, r2.Max);

        return new Range(start, end);
    }
}

public interface IVector
{
    int Size { get; }
}

/// <summary>
///     Represents a real vector with an arbitrary number of components.
/// </summary>
public class Vector : IVector
{
    private readonly double[] _values;

    public Vector(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException("Cannot initialize vector with 0 values", nameof(values));
        }

        _values = values.ToArray();
    }

    public int Size => _values.Length;

    /// <summary>
    ///     Creates a real vector from the specified values. This method will allocate a copy to retain ownership.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if no values are specified.</exception>
    public static Vector Create(params double[] values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException("Cannot initialize vector with 0 values", nameof(values));
        }

        return new Vector(values.ToArray());
    }

    private Vector(double[] values)
    {
        _values = values;
    }

    /// <summary>
    ///     Gets the number at the specified index.
    /// </summary>
    public double this[int index] => _values[index];

    public static Vector operator +(Vector a, Vector b)
    {
        Vectors.Validate(a, b);

        var values = new double[a.Size];

        for (var i = 0; i < a.Size; i++)
        {
            values[i] = a._values[i] + b._values[i];
        }

        return new Vector(values);
    }

    public static Vector operator -(Vector a, Vector b)
    {
        Vectors.Validate(a, b);

        var values = new double[a.Size];

        for (var i = 0; i < a.Size; i++)
        {
            values[i] = a._values[i] - b._values[i];
        }

        return new Vector(values);
    }

    public static Vector operator *(Vector a, double scalar)
    {
        var values = new double[a.Size];

        for (var i = 0; i < a.Size; i++)
        {
            values[i] = a._values[i] * scalar;
        }

        return new Vector(values);
    }

    public static Vector operator /(Vector a, double scalar)
    {
        var values = new double[a.Size];

        for (var i = 0; i < a.Size; i++)
        {
            values[i] = a._values[i] / scalar;
        }

        return new Vector(values);
    }

    public static double DistanceSquared(Vector a, Vector b)
    {
        Vectors.Validate(a, b);

        var result = 0d;

        for (var i = 0; i < a.Size; i++)
        {
            var d = a[i] - b[i];

            result += d * d;
        }

        return result;
    }

    public static double Distance(Vector a, Vector b)
    {
        return Math.Sqrt(DistanceSquared(a, b));
    }

    public static Vector Create(double value, int size)
    {
        var values = new double[size];

        Array.Fill(values, value);

        return new Vector(values);
    }

    public static Vector Zero(int size)
    {
        return new Vector(new double[size]);
    }

    public static bool operator ==(Vector a, Vector b) => a.Equals(b);

    public static bool operator !=(Vector a, Vector b) => !a.Equals(b);

    public override bool Equals(object? obj)
    {
        if (obj is not Vector other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Vector other)
    {
        if (Size != other.Size)
        {
            return false;
        }

        return _values.SequenceEqual(other._values);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;

            for (var i = 0; i < _values.Length; i++)
            {
                hash = hash * 31 + _values[i].GetHashCode();
            }

            return hash;
        }
    }
}

public static class Vectors
{
    // Do not use params

    /// <summary>
    ///     Ensures that the <see cref="vector"/> has the specified size.
    /// </summary>
    /// <param name="vector">The vector to check.</param>
    /// <param name="requiredSize">The required size of the vector.</param>
    /// <exception cref="InvalidOperationException">Thrown if validation failed.</exception>
    public static void Validate(IVector vector, int requiredSize)
    {
        if (vector.Size != requiredSize)
        {
            throw new InvalidOperationException($"A vector of size {requiredSize} is required.");
        }
    }

    /// <summary>
    ///     Ensures that the two vectors have the same size.
    /// </summary>
   
    /// <exception cref="Exception">Thrown if validation failed.</exception>
    public static void Validate(IVector a, IVector b)
    {
        if (a.Size != b.Size)
        {
            throw new Exception("Vectors are not compatible");
        }
    }

    public static void Validate(IVector a, IVector b, IVector c, IVector d, IVector e, IVector f)
    {
        if (a.Size != b.Size || b.Size != c.Size || c.Size != d.Size || d.Size != e.Size || e.Size != f.Size)
        {
            throw new Exception("Vectors are not compatible");
        }
    }
}