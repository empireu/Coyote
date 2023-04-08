using System.Numerics;
using GameFramework.Utilities;

namespace Coyote.App.Mathematics;

internal static class Angles
{
    public static float Modulus(float a, float n)
    {
        return (a % n + n) % n;
    }

    public static float DeltaAngle(float a, float b)
    {
        return Modulus(a - b + MathF.PI, MathF.Tau) - MathF.PI;
    }
}

interface IUnit { }
interface Displacement : IUnit { }
interface Velocity : IUnit { }
interface Acceleration : IUnit { }
interface Curvature : IUnit { }
interface Radians : IUnit { }
interface Percentage : IUnit { }

readonly struct Real<TUnit> : 
    IComparable<Real<TUnit>>,
    IComparable<float>, 
    IComparable<double>, 
    IAdditionOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    ISubtractionOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    IMultiplyOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>,
    IDivisionOperators<Real<TUnit>, Real<TUnit>, Real<TUnit>>
    where TUnit : IUnit
{
    public static readonly Real<TUnit> Zero = new(0);
    public static readonly Real<TUnit> One = new(1);
    public static readonly Real<TUnit> MinValue = new(float.MinValue);
    public static readonly Real<TUnit> MaxValue = new(float.MaxValue);

    public float Value { get; }

    public Real(float value)
    {
        Value = value;
    }

    #region Operators

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

    public static Real<TUnit> operator /(Real<TUnit> a, Real<TUnit> b)
    {
        return new Real<TUnit>(a.Value / b.Value);
    }

    public static bool operator <(Real<TUnit> a, Real<TUnit> b)
    {
        return a.Value < b.Value;
    }

    public static bool operator >(Real<TUnit> a, Real<TUnit> b)
    {
        return a.Value > b.Value;
    }

    public static bool operator <(Real<TUnit> a, float b)
    {
        return a.Value < b;
    }

    public static bool operator >(Real<TUnit> a, float b)
    {
        return a.Value > b;
    }

    public static implicit operator float(Real<TUnit> r)
    {
        return r.Value;
    }

    public static explicit operator Real<TUnit>(float f)
    {
        return new Real<TUnit>(f);
    }


    public static implicit operator double(Real<TUnit> r)
    {
        return r.Value;
    }

    public static explicit operator Real<TUnit>(double d)
    {
        return new Real<TUnit>((float)d);
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

    public override string ToString()
    {
        return $"[{Value:F4}] {typeof(TUnit).Name}";
    }

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

    public bool ApproxEquals(Real<TUnit> other, float tolerance = 10e-6f)
    {
        return ApproxEquals(this, other, tolerance);
    }

    public bool ApproxEqualsZero(float tolerance = 10e-6f)
    {
        return ApproxEquals(Zero, tolerance);
    }

    public Real<TUnit> Clamped(Real<TUnit> min, Real<TUnit> max)
    {
        return Clamp(this, min, max);
    }

    public Real<TUnit> Clamped(float min, float max)
    {
        return Clamp(this, min, max);
    }

    public Real<TUnit> Mapped(
        Real<TUnit> srcMin,
        Real<TUnit> srcMax,
        Real<TUnit> dstMin,
        Real<TUnit> dstMax)
    {
        return Map(this, srcMin, srcMax, dstMin, dstMax);
    }

    public Real<TUnit> Mapped(float srcMin, float srcMax, float dstMin, float dstMax)
    {
        return Map(this, srcMin, srcMax, dstMin, dstMax);
    }

    public Real<TOtherUnit> MappedTo<TOtherUnit>(float srcMin, float srcMax, float dstMin, float dstMax) where TOtherUnit : IUnit
    {
        return new Real<TOtherUnit>(Mapped(srcMin, srcMax, dstMin, dstMax).Value);
    }

    public Real<TOtherUnit> MappedTo<TOtherUnit>(
        Real<TUnit> srcMin,
        Real<TUnit> srcMax,
        Real<TOtherUnit> dstMin,
        Real<TOtherUnit> dstMax) where TOtherUnit : IUnit
    {
        return new Real<TOtherUnit>(Mapped(srcMin.Value, srcMax.Value, dstMin.Value, dstMax.Value).Value);
    }

    public static bool ApproxEquals(Real<TUnit> a, Real<TUnit> b, float tolerance = 10e-6f)
    {
        return a.Value.ApproxEquals(b.Value, tolerance);
    }

    public static Real<TUnit> Clamp(Real<TUnit> r, Real<TUnit> min, Real<TUnit> max)
    {
        return new Real<TUnit>(Math.Clamp(r.Value, min.Value, max.Value));
    }

    public static Real<TUnit> Clamp(Real<TUnit> r, float min, float max)
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

    public static Real<TUnit> Map(Real<TUnit> r, float srcMin, float srcMax, float dstMin, float dstMax)
    {
        return new Real<TUnit>(MathUtilities.MapRange(r.Value, srcMin, srcMax, dstMin, dstMax));
    }

    public Real<TUnit> Pow(float exponent)
    {
        return new Real<TUnit>(MathF.Pow(Value, exponent));
    }

    public Real<TUnit> Pow(Real<TUnit> exponent)
    {
        return Pow(exponent.Value);
    }
}

readonly struct Real2<TUnit> where TUnit : IUnit
{
    public static readonly Real2<TUnit> Zero = new(Real<TUnit>.Zero);
    public static readonly Real2<TUnit> One = new(Real<TUnit>.One);
    public static readonly Real2<TUnit> UnitX = new(Real<TUnit>.One, Real<TUnit>.Zero);
    public static readonly Real2<TUnit> UnitY = new(Real<TUnit>.Zero, Real<TUnit>.One);

    public Real<TUnit> X { get; }
    public Real<TUnit> Y { get; }

    public Real2(Real<TUnit> x, Real<TUnit> y)
    {
        X = x;
        Y = y;
    }

    public Real2(Real<TUnit> value) : this(value, value)
    {

    }

    public Real2(float x, float y) : this(new Real<TUnit>(x), new Real<TUnit>(y))
    {

    }

    #region Operators

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

    public static Real2<TUnit> operator /(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real2<TUnit>(a.X / b.X, a.Y / b.Y);
    }

    public static Real2<TUnit> operator /(Real2<TUnit> a, Real<TUnit> scalar)
    {
        return new Real2<TUnit>(a.X / scalar, a.Y / scalar);
    }

    public static implicit operator Vector2(Real2<TUnit> r2)
    {
        return new Vector2(r2.X.Value, r2.Y.Value);
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

    public bool ApproxEquals(Real2<TUnit> other, float tolerance = 10e-6f)
    {
        return X.ApproxEquals(other.X, tolerance) && Y.ApproxEquals(other.Y, tolerance);
    }

    public bool ApproxEqualsZero(float tolerance = 10e-6f)
    {
        return ApproxEquals(Zero, tolerance);
    }

   
    public Real2<TUnit> Normalized()
    {
        return Normalize(this);
    }

    public Vector2 ToV2()
    {
        return (Vector2)this;
    }
    public Real<TUnit> LengthSquared()
    {
        return LengthSquared(this);
    }

    public Real<TUnit> Length()
    {
        return Length(this);
    }

    public static Real<TUnit> DistanceSquared(Real2<TUnit> a, Real2<TUnit> b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;

        return dx * dx + dy * dy;
    }

    public static Real<TUnit> Distance(Real2<TUnit> a, Real2<TUnit> b)
    {
        return new Real<TUnit>(MathF.Sqrt(DistanceSquared(a, b).Value));
    }

    public static Real<TUnit> LengthSquared(Real2<TUnit> r)
    {
        return r.X * r.X + r.Y * r.Y;
    }

    public static Real<TUnit> Length(Real2<TUnit> r)
    {
        return new Real<TUnit>(MathF.Sqrt(LengthSquared(r).Value));
    }

    public static Real2<TUnit> Normalize(Real2<TUnit> v)
    {
        return v / v.Length();
    }
}