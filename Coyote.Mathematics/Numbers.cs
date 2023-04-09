using System.Numerics;
using System.Text.Json.Serialization;
using GameFramework.Utilities;

namespace Coyote.Mathematics;

public interface IUnit { }

/// <summary>
///     Identifies a Displacement (Distance) unit.
/// </summary>
public interface Displacement : IUnit { }

/// <summary>
///     Identifies a Velocity (<see cref="Displacement"/>') unit.
/// </summary>
public interface Velocity : IUnit { }

/// <summary>
///     Identifies an Angular Velocity (<see cref="Radians"/>') unit.
/// </summary>
public interface AngularVelocity : IUnit { }

/// <summary>
///     Identifies a Centripetal Acceleration (<see cref="Velocity"/>²/<see cref="Curvature"/>) unit.
/// </summary>
public interface CentripetalAcceleration : IUnit { }

/// <summary>
///     Identifies an Acceleration (<see cref="Displacement"/>'', <see cref="Velocity"/>') unit.
/// </summary>
public interface Acceleration : IUnit { }

/// <summary>
///     Identifies an Angular Acceleration (<see cref="Radians"/>'', <see cref="AngularVelocity"/>') unit.
/// </summary>
public interface AngularAcceleration : IUnit { }

/// <summary>
///     Identifies a Curvature (<see cref="Radians"/>/<see cref="Displacement"/>) unit.
/// </summary>
public interface Curvature : IUnit { }

/// <summary>
///     Identifies a rotation unit, expressed in radians.
/// </summary>
public interface Radians : IUnit { }

/// <summary>
///     Identifies a rotation unit, expressed in degrees.
/// </summary>
public interface Degrees : IUnit { }

/// <summary>
///     Identifies a percentage unit, expressed in a value ranging from 0-1.
/// </summary>
public interface Percentage : IUnit { }

/// <summary>
///     Identifies a time unit, expressed in seconds.
/// </summary>
public interface Time : IUnit { }

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
    where TUnit : IUnit
{
    public static readonly Real<TUnit> Zero = new(0);
    public static readonly Real<TUnit> One = new(1);
    public static readonly Real<TUnit> MinValue = new(double.MinValue);
    public static readonly Real<TUnit> MaxValue = new(double.MaxValue);

    [JsonInclude]
    public double Value { get; }

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

    public override string ToString()
    {
        return $"{Value:F4} {typeof(TUnit).Name}";
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
    public Real<TOtherUnit> MappedTo<TOtherUnit>(double srcMin, double srcMax, double dstMin, double dstMax) where TOtherUnit : IUnit
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
        Real<TOtherUnit> dstMax) where TOtherUnit : IUnit
    {
        return new Real<TOtherUnit>(Mapped(srcMin.Value, srcMax.Value, dstMin.Value, dstMax.Value).Value);
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
}

public readonly struct Real2<TUnit> where TUnit : IUnit
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
}