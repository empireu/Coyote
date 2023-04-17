using System.Numerics;
using System.Text.Json.Serialization;
using GameFramework.Utilities;

namespace Coyote.Mathematics;

#region Units

public interface Displacement { }
public interface Velocity { }
public interface Acceleration { }
public interface AngularDisplacement { }
public interface AngularVelocity { }
public interface AngularAcceleration { }
public interface AngleDegrees { }
public interface CentripetalAcceleration { }
public interface Curvature { }
public interface Percentage { }
public interface Time { }

#endregion

/// <summary>
///     Represents a real number with a symbolic unit.
/// </summary>
/// <typeparam name="TUnit">A symbolic unit associated with this number.</typeparam>
public readonly struct Real<TUnit> : 
    IComparable<Real<TUnit>>,
    IComparable<double>, 
    IUnaryPlusOperators<Real<TUnit>, Real<TUnit>>,
    IUnaryNegationOperators<Real<TUnit>, Real<TUnit>>,
    IAdditionOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    ISubtractionOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    IMultiplyOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    IMultiplyOperators<Real<TUnit>, double, Real<TUnit>>,
    IDivisionOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    IDivisionOperators<Real<TUnit>, double, Real<TUnit>>
{
    public static readonly Real<TUnit> Zero = new(0);
    public static readonly Real<TUnit> One = new(1);
    public static readonly Real<TUnit> MinValue = new(double.MinValue);
    public static readonly Real<TUnit> MaxValue = new(double.MaxValue);

    [JsonInclude]
    public double Value { get; }

    [JsonIgnore] public bool IsNan => double.IsNaN(Value);
    [JsonIgnore] public bool IsInf => double.IsInfinity(Value);
    [JsonIgnore] public bool IsRealFinite => !IsNan && !IsInf;
    [JsonIgnore] public int Sign => Math.Sign(Value);

    [JsonConstructor]
    public Real(double value)
    {
        Value = value;
    }

    #region Operators

    public static Real<TUnit> operator +(Real<TUnit> r)
    {
        return new Real<TUnit>(+r.Value);
    }

    public static Real<TUnit> operator -(Real<TUnit> r)
    {
        return new Real<TUnit>(-r.Value);
    }

    public static Real<TUnit> operator +(Real<TUnit> a, Real<TUnit> b)
    {
        return new Real<TUnit>(a.Value + b.Value);
    }

    public static Real<TUnit> operator -(Real<TUnit> a, Real<TUnit> b)
    {
        return new Real<TUnit>(a.Value - b.Value);
    }

    public static Real<TUnit> operator *(Real<TUnit> a, Real<TUnit> b)
    {
        return new Real<TUnit>(a.Value * b.Value);
    }
    
    public static Real<TUnit> operator *(Real<TUnit> a, double b)
    {
        return new Real<TUnit>(a.Value * b);
    }
    
    public static Real<TUnit> operator /(Real<TUnit> a, Real<TUnit> b)
    {
        return new Real<TUnit>(a.Value / b.Value);
    }

    public static Real<TUnit> operator /(Real<TUnit> a, double b)
    {
        return new Real<TUnit>(a.Value / b);
    }

    public static bool operator <(Real<TUnit> a, Real<TUnit> b)
    {
        return a.Value < b.Value;
    }

    public static bool operator >(Real<TUnit> a, Real<TUnit> b)
    {
        return a.Value > b.Value;
    }

    public static bool operator <(Real<TUnit> a, double b)
    {
        return a.Value < b;
    }

    public static bool operator >(Real<TUnit> a, double b)
    {
        return a.Value > b;
    }

    public static implicit operator double(Real<TUnit> r)
    {
        return r.Value;
    }

    public static explicit operator Real<TUnit>(double f)
    {
        return new Real<TUnit>(f);
    }

    public static bool operator ==(Real<TUnit> a, Real<TUnit> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Real<TUnit> a, Real<TUnit> b)
    {
        return !a.Equals(b);
    }

    #endregion

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(Real<TUnit> other)
    {
        return Value.CompareTo(other.Value);
    }

    public int CompareTo(float other)
    {
        return Value.CompareTo(other);
    }

    public int CompareTo(double other)
    {
        return Value.CompareTo(other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Real<TUnit> other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Real<TUnit> other)
    {
        return other.Value.Equals(Value);
    }

    public bool ApproxEquals(Real<TUnit> other, double tolerance = 10e-6)
    {
        return ApproxEquals(this, other, tolerance);
    }

    public bool ApproxEqualsZero(double tolerance = 10e-6)
    {
        return ApproxEquals(Zero, tolerance);
    }

    public static bool ApproxEquals(Real<TUnit> a, Real<TUnit> b, double tolerance = 10e-6)
    {
        return a.Value.ApproxEquals(b.Value, tolerance);
    }

    public string ToStringFormatted()
    {
        return $"{Value:F4} {typeof(TUnit).Name}";
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    /// <summary>
    ///     Returns a <see cref="Real{TUnit}"/> clamped to the specified <see cref="min"/>-<see cref="max"/> range. Internally, this calls <see cref="Clamp(Coyote.Mathematics.Real{TUnit},Coyote.Mathematics.Real{TUnit},Coyote.Mathematics.Real{TUnit})"/>
    /// </summary>
    /// <param name="min">The minimum allowed value of <see cref="Value"/>.</param>
    /// <param name="max">The maximum allowed value of <see cref="Value"/>.</param>
    /// <returns></returns>
    public Real<TUnit> Clamped(Real<TUnit> min, Real<TUnit> max)
    {
        return Clamp(this, min, max);
    }

    /// <summary>
    ///     Returns a <see cref="Real{TUnit}"/> clamped to the specified <see cref="min"/>-<see cref="max"/> range. Internally, this calls <see cref="Clamp(Coyote.Mathematics.Real{TUnit},double,double)"/>
    /// </summary>
    /// <param name="min">The minimum allowed value of <see cref="Value"/>.</param>
    /// <param name="max">The maximum allowed value of <see cref="Value"/>.</param>
    /// <returns></returns>
    public Real<TUnit> Clamped(double min, double max)
    {
        return Clamp(this, min, max);
    }

    public Real<TUnit> Abs()
    {
        return new Real<TUnit>(Math.Abs(Value));
    }

    /// <summary>
    ///     Maps the <see cref="Value"/> from the source range <see cref="srcMin"/>-<seealso cref="srcMax"/> to the destination range <see cref="dstMin"/>-<see cref="dstMax"/>
    ///     Internally, this calls <see cref="Map(Coyote.Mathematics.Real{TUnit},Coyote.Mathematics.Real{TUnit},Coyote.Mathematics.Real{TUnit},Coyote.Mathematics.Real{TUnit},Coyote.Mathematics.Real{TUnit})"/>
    /// </summary>
    /// <param name="srcMin">The lower boundary of the source range.</param>
    /// <param name="srcMax">The upper boundary of the source range.</param>
    /// <param name="dstMin">The lower boundary of the destination range.</param>
    /// <param name="dstMax">The upper boundary of the destination range.</param>
    /// <returns>The <see cref="Value"/>, mapped using the specified ranges.</returns>
    public Real<TUnit> Mapped(
        Real<TUnit> srcMin,
        Real<TUnit> srcMax,
        Real<TUnit> dstMin,
        Real<TUnit> dstMax)
    {
        return Map(this, srcMin, srcMax, dstMin, dstMax);
    }

    /// <summary>
    ///     Maps the <see cref="Value"/> from the source range <see cref="srcMin"/>-<seealso cref="srcMax"/> to the destination range <see cref="dstMin"/>-<see cref="dstMax"/>
    ///     Internally, this calls <see cref="Map(Coyote.Mathematics.Real{TUnit},double,double,double,double)"/>
    /// </summary>
    /// <param name="srcMin">The lower boundary of the source range.</param>
    /// <param name="srcMax">The upper boundary of the source range.</param>
    /// <param name="dstMin">The lower boundary of the destination range.</param>
    /// <param name="dstMax">The upper boundary of the destination range.</param>
    /// <returns>The <see cref="Value"/>, mapped using the specified ranges.</returns>
    public Real<TUnit> Mapped(double srcMin, double srcMax, double dstMin, double dstMax)
    {
        return Map(this, srcMin, srcMax, dstMin, dstMax);
    }

    /// <summary>
    ///     Maps the <see cref="Value"/> from the source range <see cref="srcMin"/>-<seealso cref="srcMax"/> to the destination range <see cref="dstMin"/>-<see cref="dstMax"/>
    ///     This is assumed to convert <see cref="TUnit"/> to <see cref="TOtherUnit"/>.
    /// </summary>
    /// <param name="srcMin">The lower boundary of the source range.</param>
    /// <param name="srcMax">The upper boundary of the source range.</param>
    /// <param name="dstMin">The lower boundary of the destination range.</param>
    /// <param name="dstMax">The upper boundary of the destination range.</param>
    /// <returns>The <see cref="Value"/>, mapped using the specified ranges.</returns>
    public Real<TOtherUnit> MappedTo<TOtherUnit>(double srcMin, double srcMax, double dstMin, double dstMax)
    {
        return new Real<TOtherUnit>(Mapped(srcMin, srcMax, dstMin, dstMax).Value);
    }

    /// <summary>
    ///     Maps the <see cref="Value"/> from the source range <see cref="srcMin"/>-<seealso cref="srcMax"/> to the destination range <see cref="dstMin"/>-<see cref="dstMax"/>
    ///     This is assumed to convert <see cref="TUnit"/> to <see cref="TOtherUnit"/>.
    /// </summary>
    /// <param name="srcMin">The lower boundary of the source range.</param>
    /// <param name="srcMax">The upper boundary of the source range.</param>
    /// <param name="dstMin">The lower boundary of the destination range.</param>
    /// <param name="dstMax">The upper boundary of the destination range.</param>
    /// <returns>The <see cref="Value"/>, mapped using the specified ranges.</returns>
    public Real<TOtherUnit> MappedTo<TOtherUnit>(
        Real<TUnit> srcMin,
        Real<TUnit> srcMax,
        Real<TOtherUnit> dstMin,
        Real<TOtherUnit> dstMax)
    {
        return new Real<TOtherUnit>(Mapped(srcMin.Value, srcMax.Value, dstMin.Value, dstMax.Value).Value);
    }

    public Real<TUnit> MinWith(Real<TUnit> other)
    {
        return Min(this, other);
    }

    /// <summary>
    ///     Returns the value <see cref="r"/>, clamped to the range <see cref="min"/>-<see cref="max"/>.
    /// </summary>
    /// <param name="r">The value to clamp.</param>
    /// <param name="min">The minimum the value <see cref="r"/> can be.</param>
    /// <param name="max">The maximum the value <see cref="r"/> can be.</param>
    /// <returns>The value <see cref="r"/>, clamped to the specified range.</returns>
    public static Real<TUnit> Clamp(Real<TUnit> r, Real<TUnit> min, Real<TUnit> max)
    {
        return new Real<TUnit>(Math.Clamp(r.Value, min.Value, max.Value));
    }

    /// <summary>
    ///     Returns the value <see cref="r"/>, clamped to the range <see cref="min"/>-<see cref="max"/>.
    /// </summary>
    /// <param name="r">The value to clamp.</param>
    /// <param name="min">The minimum the value <see cref="r"/> can be.</param>
    /// <param name="max">The maximum the value <see cref="r"/> can be.</param>
    /// <returns>The value <see cref="r"/>, clamped to the specified range.</returns>
    public static Real<TUnit> Clamp(Real<TUnit> r, double min, double max)
    {
        return new Real<TUnit>(Math.Clamp(r.Value, min, max));
    }

    public static Real<TUnit> Map(
        Real<TUnit> r, 
        Real<TUnit> srcMin, 
        Real<TUnit> srcMax, 
        Real<TUnit> dstMin,
        Real<TUnit> dstMax)
    {
        return new Real<TUnit>(MathUtilities.MapRange(r.Value, srcMin.Value, srcMax.Value, dstMin.Value, dstMax.Value));
    }

    public static Real<TUnit> Lerp(Real<TUnit> a, Real<TUnit> b, Real<TUnit> t)
    {
        return new Real<TUnit>(MathExt.Lerp(a, b, t));
    }

    public static Real<TUnit> Lerp(Real<TUnit> a, Real<TUnit> b, double t)
    {
        return new Real<TUnit>(MathExt.Lerp(a, b, t));
    }

    public static Real<TUnit> Map(Real<TUnit> r, double srcMin, double srcMax, double dstMin, double dstMax)
    {
        return new Real<TUnit>(MathUtilities.MapRange(r.Value, srcMin, srcMax, dstMin, dstMax));
    }

    public Real<TUnit> Pow(double exponent)
    {
        return new Real<TUnit>(Math.Pow(Value, exponent));
    }

    public Real<TUnit> Pow(Real<TUnit> exponent)
    {
        return Pow(exponent.Value);
    }

    public static Real<TUnit> Min(Real<TUnit> a, Real<TUnit> b)
    {
        return Math.Min(a.Value, b.Value).ToReal<TUnit>();
    }

    public static Real<TUnit> Max(Real<TUnit> a, Real<TUnit> b)
    {
        return Math.Max(a.Value, b.Value).ToReal<TUnit>();
    }
}

/// <summary>
///     Represents a vector with two <see cref="Real{TUnit}"/> components.
/// </summary>
public readonly struct Real2<TUnit>
{
    public static readonly Real2<TUnit> Zero = new(Real<TUnit>.Zero);
    public static readonly Real2<TUnit> One = new(Real<TUnit>.One);
    public static readonly Real2<TUnit> UnitX = new(Real<TUnit>.One, Real<TUnit>.Zero);
    public static readonly Real2<TUnit> UnitY = new(Real<TUnit>.Zero, Real<TUnit>.One);

    [JsonInclude]
    public Real<TUnit> X { get; }

    [JsonInclude]
    public Real<TUnit> Y { get; }

    [JsonConstructor]
    public Real2(Real<TUnit> x, Real<TUnit> y)
    {
        X = x;
        Y = y;
    }

    public Real2(Real<TUnit> value) : this(value, value)
    {

    }

    public Real2(double x, double y) : this(new Real<TUnit>(x), new Real<TUnit>(y))
    {

    }

    public Real2(Vector2 vector) : this(vector.X, vector.Y)
    {

    }

    #region Operators

    public static Real2<TUnit> operator +(Real2<TUnit> r)
    {
        return new Real2<TUnit>(+r.X, +r.Y);
    }

    public static Real2<TUnit> operator -(Real2<TUnit> r)
    {
        return new Real2<TUnit>(-r.X, -r.Y);
    }

    public static Real2<TUnit> operator +(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real2<TUnit>(a.X + b.X, a.Y + b.Y);
    }

    public static Real2<TUnit> operator -(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real2<TUnit>(a.X - b.X, a.Y - b.Y);
    }

    public static Real2<TUnit> operator *(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real2<TUnit>(a.X * b.X, a.Y * b.Y);
    }

    public static Real2<TUnit> operator *(Real2<TUnit> a, Real<TUnit> scalar)
    {
        return new Real2<TUnit>(a.X * scalar, a.Y * scalar);
    }

    public static Real2<TUnit> operator *(Real2<TUnit> a, double scalar)
    {
        return new Real2<TUnit>(a.X * scalar, a.Y * scalar);
    }

    public static Real2<TUnit> operator /(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real2<TUnit>(a.X / b.X, a.Y / b.Y);
    }

    public static Real2<TUnit> operator /(Real2<TUnit> a, Real<TUnit> scalar)
    {
        return new Real2<TUnit>(a.X / scalar, a.Y / scalar);
    }

    public static Real2<TUnit> operator /(Real2<TUnit> a, double scalar)
    {
        return new Real2<TUnit>(a.X / scalar, a.Y / scalar);
    }

    public static implicit operator Vector2(Real2<TUnit> r2)
    {
        return new Vector2((float)r2.X.Value, (float)r2.Y.Value);
    }

    public static explicit operator Real2<TUnit>(Vector2 v)
    {
        return new Real2<TUnit>(new Real<TUnit>(v.X), new Real<TUnit>(v.Y));
    }

    public static bool operator ==(Real2<TUnit> a, Real2<TUnit> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Real2<TUnit> a, Real2<TUnit> b)
    {
        return !a.Equals(b);
    }

    public static bool operator <(Real2<TUnit> a, Real2<TUnit> b)
    {
        return a.LengthSquared() < b.LengthSquared();
    }

    public static bool operator >(Real2<TUnit> a, Real2<TUnit> b)
    {
        return a.LengthSquared() > b.LengthSquared();
    }

    #endregion

    public override string ToString()
    {
        return $"[X={X.Value:F4} Y={Y.Value:F4}] {typeof(TUnit).Name}";
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Real2<TUnit> other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Real2<TUnit> other)
    {
        return other.X.Equals(X) && other.Y.Equals(Y);
    }

    public bool ApproxEquals(Real2<TUnit> other, double tolerance = 10e-6)
    {
        return X.ApproxEquals(other.X, tolerance) && Y.ApproxEquals(other.Y, tolerance);
    }

    public bool ApproxEqualsZero(double tolerance = 10e-6)
    {
        return ApproxEquals(Zero, tolerance);
    }

    /// <returns>A unit <see cref="Real2{TUnit}"/> (a vector with the length 1) from this <see cref="Real2{TUnit}"/></returns>
    public Real2<TUnit> Normalized()
    {
        return Normalize(this);
    }

    /// <returns>The squared length of this vector.</returns>
    public Real<TUnit> LengthSquared()
    {
        return LengthSquared(this);
    }

    /// <returns>The length of this vector.</returns>
    public Real<TUnit> Length()
    {
        return Length(this);
    }

    /// <summary>
    ///     Computes the squared distance between the two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The squared distance between the two points.</returns>
    public static Real<TUnit> DistanceSquared(Real2<TUnit> a, Real2<TUnit> b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;

        return dx * dx + dy * dy;
    }

    /// <summary>
    ///     Computes the distance between the two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance between the two points.</returns>
    public static Real<TUnit> Distance(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real<TUnit>(Math.Sqrt(DistanceSquared(a, b).Value));
    }

    /// <returns>The squared length of <see cref="r"/>.</returns>
    public static Real<TUnit> LengthSquared(Real2<TUnit> r)
    {
        return r.X * r.X + r.Y * r.Y;
    }

    /// <returns>The length of <see cref="r"/>.</returns>
    public static Real<TUnit> Length(Real2<TUnit> r)
    {
        return new Real<TUnit>(Math.Sqrt(LengthSquared(r).Value));
    }

    /// <summary>
    ///     Normalizes <see cref="v"/> (so that its length is 1).
    /// </summary>
    /// <param name="v">The vector to normalize.</param>
    /// <returns>A direction vector, with the length 1.</returns>
    public static Real2<TUnit> Normalize(Real2<TUnit> v)
    {
        return v / v.Length();
    }

    public static Real2<TUnit> Lerp(Real2<TUnit> a, Real2<TUnit> b, double t)
    {
        return new Real2<TUnit>(
            Real<TUnit>.Lerp(a.X, b.X, t),
            Real<TUnit>.Lerp(a.Y, b.Y, t)
        );
    }

    public static Real2<TUnit> Lerp(Real2<TUnit> a, Real2<TUnit> b, Real<Percentage> t)
    {
        return new Real2<TUnit>(
            Real<TUnit>.Lerp(a.X, b.X, t),
            Real<TUnit>.Lerp(a.Y, b.Y, t)
        );
    }
}

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

public interface IRealVector
{
    int Size { get; }
}

/// <summary>
///     Represents a real vector with an arbitrary number of components.
/// </summary>
public readonly struct RealVector<TUnit> : IRealVector
{
    private readonly double[] _values;

    public RealVector(ReadOnlySpan<double> values)
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
    public static RealVector<TUnit> Create(params double[] values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException("Cannot initialize vector with 0 values", nameof(values));
        }

        return new RealVector<TUnit>(values.ToArray());
    }

    /// <summary>
    ///     Creates a real vector from the specified values. This method will allocate a copy to retain ownership.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if no values are specified.</exception>
    public RealVector(params Real<TUnit>[] values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException("Cannot initialize vector with 0 values", nameof(values));
        }

        _values = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            _values[i] = values[i];
        }
    }

    private RealVector(double[] values)
    {
        _values = values;
    }

    /// <summary>
    ///     Gets the number at the specified index.
    /// </summary>
    public Real<TUnit> this[int index] => new(_values[index]);

    public static RealVector<TUnit> operator +(RealVector<TUnit> a, RealVector<TUnit> b)
    {
        Vectors.Validate(a, b);

        var values = new double[a.Size];

        for (var i = 0; i < a.Size; i++)
        {
            values[i] = a._values[i] + b._values[i];
        }

        return new RealVector<TUnit>(values);
    }

    public static RealVector<TUnit> operator -(RealVector<TUnit> a, RealVector<TUnit> b)
    {
        Vectors.Validate(a, b);

        var values = new double[a.Size];

        for (var i = 0; i < a.Size; i++)
        {
            values[i] = a._values[i] - b._values[i];
        }

        return new RealVector<TUnit>(values);
    }

    public static Real<TUnit> DistanceSquared(RealVector<TUnit> a, RealVector<TUnit> b)
    {
        Vectors.Validate(a, b);

        var result = 0d;

        for (var i = 0; i < a.Size; i++)
        {
            var d = a[i] - b[i];

            result += d * d;
        }

        return new Real<TUnit>(result);
    }

    public static Real<TUnit> Distance(RealVector<TUnit> a, RealVector<TUnit> b)
    {
        return Math.Sqrt(DistanceSquared(a, b)).ToReal<TUnit>();
    }

    public RealVector<TOther> To<TOther>()
    {
        return new RealVector<TOther>(_values);
    }

    public static RealVector<TUnit> Create(Real<TUnit> value, int size)
    {
        var values = new double[size];

        Array.Fill(values, value);

        return new RealVector<TUnit>(values);
    }

    public static RealVector<TUnit> Zero(int size)
    {
        return new RealVector<TUnit>(new double[size]);
    }
}

public static class Vectors
{
    /// <summary>
    ///     Ensures that the <see cref="vector"/> has the specified size.
    /// </summary>
    /// <param name="vector">The vector to check.</param>
    /// <param name="requiredSize">The required size of the vector.</param>
    /// <exception cref="InvalidOperationException">Thrown if validation failed.</exception>
    public static void Validate(IRealVector vector, int requiredSize)
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
    public static void Validate(IRealVector a, IRealVector b)
    {
        if (a.Size != b.Size)
        {
            throw new Exception("Vectors are not compatible");
        }
    }

    /// <summary>
    ///     Ensures that all vectors have the same size.
    /// </summary>
    /// <exception cref="Exception">Thrown if validation failed.</exception>
    public static void ValidateMany(params IRealVector[] vectors)
    {
        if (vectors.Length <= 1)
        {
            return;
        }

        var size = vectors[0].Size;

        for (var i = 1; i < vectors.Length; i++)
        {
            if (vectors[i].Size != size)
            {
                throw new Exception("Invalid vector set");
            }
        }
    }
}