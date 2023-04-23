using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

/// <summary>
///     Describes an angle of rotation.
/// </summary>
public readonly struct Rotation
{
    public static readonly Rotation Zero = new(Real<AngularDisplacement>.Zero);

    [JsonInclude]
    public Real<AngularDisplacement> Angle { get; }

    [JsonIgnore]
    public Real<Displacement> Cos { get; }

    [JsonIgnore]
    public Real<Displacement> Sin { get; }

    [JsonIgnore]
    public double Tangent => Sin.Value / Cos.Value;

    [JsonConstructor]
    public Rotation(Real<AngularDisplacement> angle)
    {
        Angle = angle;

        Cos = Math.Cos(angle).ToReal<Displacement>();
        Sin = Math.Sin(angle).ToReal<Displacement>();
    }

    public Rotation(double angle) : this(angle.ToReal<AngularDisplacement>())
    {

    }

    public static Rotation FromDirection(Real2<Displacement> direction)
    {
        return new Rotation(Math.Atan2(direction.Y, direction.X).ToReal<AngularDisplacement>());
    }

    public static Rotation FromDirection(Real<Displacement> x, Real<Displacement> y)
    {
        return new Rotation(Math.Atan2(y, x).ToReal<AngularDisplacement>());
    }

    public static Rotation FromDirection(double x, double y)
    {
        return new Rotation(Math.Atan2(y, x).ToReal<AngularDisplacement>());
    }

    public Rotation Rotated(Rotation other)
    {
        return FromDirection(new Real2<Displacement>(Cos * other.Cos - Sin * other.Sin, Cos * other.Sin + Sin * other.Cos));
    }

    #region Operators

    public static Rotation operator +(Rotation r)
    {
        return new Rotation(+r.Angle);
    }

    public static Rotation operator -(Rotation r)
    {
        return new Rotation(-r.Angle);
    }

    public static Rotation operator +(Rotation a, Rotation b)
    {
        return a.Rotated(b);
    }

    public static Rotation operator -(Rotation a, Rotation b)
    {
        return a.Rotated(-b);
    }

    public static Rotation operator *(Rotation a, double scalar)
    {
        return new Rotation(new Real<AngularDisplacement>(a.Angle.Value * scalar));
    }

    public static Rotation operator /(Rotation a, double scalar)
    {
        return new Rotation(new Real<AngularDisplacement>(a.Angle.Value / scalar));
    }

    public static implicit operator double(Rotation r)
    {
        return r.Angle.Value;
    }

    public static implicit operator float(Rotation r)
    {
        return (float)r.Angle.Value;
    }

    public static implicit operator Real<AngularDisplacement>(Rotation a)
    {
        return a.Angle;
    }

    public static implicit operator Rotation(Real<AngularDisplacement> a)
    {
        return new Rotation(a);
    }

    public static explicit operator Rotation(double a)
    {
        return new Rotation(a.ToReal<AngularDisplacement>());
    }

    public static explicit operator Rotation(float a)
    {
        return new Rotation(a.ToReal<AngularDisplacement>());
    }

    public static bool operator ==(Rotation a, Rotation b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Rotation a, Rotation b)
    {
        return !a.Equals(b);
    }

    #endregion

    public override string ToString()
    {
        return $"Rotation [A={Angle.ToDegrees()}deg, Cos={Cos}, Sin={Sin}, Tan={Tangent}]";
    }

    public override int GetHashCode()
    {
        return Angle.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Rotation other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Rotation other)
    {
        return Angle.Equals(other.Angle);
    }

    public bool ApproxEquals(Rotation other, double tolerance = 10e-6f)
    {
        return Angle.ApproxEquals(other.Angle, tolerance);
    }

    public bool ApproxEqualsZero(Rotation other, double tolerance = 10e-6f)
    {
        return Angle.ApproxEqualsZero(tolerance);
    }

    public static Rotation Lerp(Rotation a, Rotation b, double t)
    {
        return new Rotation(Math.Atan2(Real<Displacement>.Lerp(a.Sin, b.Sin, t), Real<Displacement>.Lerp(a.Cos, b.Cos, t)));
    }
}