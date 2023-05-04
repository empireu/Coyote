using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

// todo rewrite these classes

/// <summary>
///     Describes an angle of rotation.
/// </summary>
public readonly struct Rotation
{
    public static readonly Rotation Zero = new(0.0);

    [JsonInclude]
    public double Angle { get; }

    [JsonIgnore]
    public double Cos { get; }

    [JsonIgnore]
    public double Sin { get; }

    [JsonIgnore]
    public double Tangent => Sin / Cos;

    [JsonConstructor]
    public Rotation(double angle)
    {
        Angle = angle;

        Cos = Math.Cos(angle);
        Sin = Math.Sin(angle);
    }


    public static Rotation Exp(Vector2d direction)
    {
        return new Rotation(Math.Atan2(direction.Y, direction.X));
    }

    public static Rotation Exp(double x, double y)
    {
        return new Rotation(Math.Atan2(y, x));
    }

    public Rotation Rotated(Rotation other)
    {
        return Exp(new Vector2d(Cos * other.Cos - Sin * other.Sin, Cos * other.Sin + Sin * other.Cos));
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
        return new Rotation((a.Angle * scalar));
    }

    public static Rotation operator /(Rotation a, double scalar)
    {
        return new Rotation(a.Angle / scalar);
    }

    public static implicit operator double(Rotation r)
    {
        return r.Angle;
    }

    public static implicit operator float(Rotation r)
    {
        return (float)r.Angle;
    }



    public static explicit operator Rotation(double a)
    {
        return new Rotation(a);
    }

    public static explicit operator Rotation(float a)
    {
        return new Rotation(a);
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
        return $"Rotation [A={Angle}deg, Cos={Cos}, Sin={Sin}, Tan={Tangent}]";
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

    public static Rotation Lerp(Rotation a, Rotation b, double t)
    {
        return new Rotation(Math.Atan2(MathExt.Lerp(a.Sin, b.Sin, t), MathExt.Lerp(a.Cos, b.Cos, t)));
    }
}