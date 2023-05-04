using System.Numerics;
using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

public readonly struct Translation
{
    public static readonly Translation Zero = new(0, 0);
    public static readonly Translation One = new(1, 1);
    public static readonly Translation UnitX = new(1, 0);
    public static readonly Translation UnitY = new(0, 1);

    [JsonInclude]
    public Vector2d Displacement { get; }

    [JsonIgnore]
    public double X => Displacement.X;
    
    [JsonIgnore]
    public double Y => Displacement.Y;

    [JsonConstructor]
    public Translation(Vector2d displacement)
    {
        Displacement = displacement;
    }

    public Translation(double x, double y) : this(new Vector2d(x, y))
    {

    }

    public Translation(Vector2 vector): this(vector.X, vector.Y)
    {

    }

    #region Operators

    public static Translation operator +(Translation a)
    {
        return new Translation(+a.Displacement);
    }

    public static Translation operator -(Translation a)
    {
        return new Translation(-a.Displacement);
    }

    public static Translation operator +(Translation a, Translation b)
    {
        return new Translation(a.Displacement + b.Displacement);
    }

    public static Translation operator +(Translation a, Vector2d b)
    {
        return new Translation(a.Displacement + b);
    }

 
    public static Translation operator -(Translation a, Translation b)
    {
        return new Translation(a.Displacement - b.Displacement);
    }

    public static Translation operator -(Translation a, Vector2d b)
    {
        return new Translation(a.Displacement - b);
    }

   
    
    public static Translation operator *(Translation a, Translation b)
    {
        return new Translation(a.Displacement * b.Displacement);
    }

    public static Translation operator *(Translation a, Vector2d b)
    {
        return new Translation(a.Displacement * b);
    }

  
    public static Translation operator *(Translation a, double scalar)
    {
        return new Translation(a.Displacement * scalar);
    }

    public static Translation operator /(Translation a, Translation b)
    {
        return new Translation(a.Displacement / b.Displacement);
    }

    public static Translation operator /(Translation a, Vector2d b)
    {
        return new Translation(a.Displacement / b);
    }

  
    public static Translation operator /(Translation a, double scalar)
    {
        return new Translation(a.Displacement / scalar);
    }

 
  

    public static implicit operator Vector2(Translation a)
    {
        return a.Displacement;
    }

    #endregion

    public Translation Rotated(Rotation rotation)
    {
        return new Translation(
            X * rotation.Cos - Y * rotation.Sin, 
            X * rotation.Sin + Y * rotation.Cos);
    }

    public override int GetHashCode()
    {
        return Displacement.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Translation other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Translation other)
    {
        return Displacement.Equals(other.Displacement);
    }

    public override string ToString()
    {
        return $"Translation [{Displacement}]";
    }

    public static Translation Lerp(Translation a, Translation b, double t)
    {
        return new Translation(Vector2d.Lerp(a.Displacement, b.Displacement, t));
    }
}
