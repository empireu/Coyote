using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

/// <summary>
///     A <see cref="Pose"/> is comprised of the <see cref="Translation"/> and <see cref="Rotation"/> of an object in a 2D world.
/// </summary>
public readonly struct Pose
{
    public static readonly Pose Zero = new(0, 0, 0f);

    [JsonInclude]
    public Translation Translation { get; }

    [JsonInclude]
    public Rotation Rotation { get; }

    /// <summary>
    ///     Gets the rotation direction of this pose.
    ///     <remarks>This will issue <see cref="Math.Cos"/> and <see cref="Math.Sin"/> calls.</remarks>
    /// </summary>
    [JsonIgnore]
    public Real2<Displacement> RotationVector => new(Math.Cos(Rotation), Math.Sin(Rotation));

    [JsonConstructor]
    public Pose(Translation translation, Rotation rotation)
    {
        Translation = translation;
        Rotation = rotation;
    }

    public Pose(Translation translation, Real<Angle> rotation)
    {
        Translation = translation;
        Rotation = rotation;
    }

    public Pose(Real2<Displacement> translation, Real<Angle> rotation)
    {
        Translation = translation;
        Rotation = rotation;
    }

    public Pose(double x, double y, double rotation) : this(new Translation(x, y), new Rotation(rotation))
    {

    }

    public Pose(Vector2 translation, double rotation) : this(translation.X, translation.Y, rotation)
    {

    }

    public Pose(Real2<Displacement> displacement, double rotation) : this(displacement.X, displacement.Y, rotation)
    {

    }

    /// <summary>
    ///     Computes a pose using the specified <see cref="Displacement"/> and a <see cref="tangent"/> vector.
    /// </summary>
    /// <param name="displacement">The displacement to use.</param>
    /// <param name="tangent">The direction vector of the pose.</param>
    public Pose(Real2<Displacement> displacement, Real2<Displacement> tangent) : this(new Translation(displacement), Rotation.FromDirection(tangent))
    {

    }

    #region Operators

    public static Pose operator +(Pose a, Pose b)
    {
        return new Pose(a.Translation + b.Translation, a.Rotation + b.Rotation);
    }

    public static Pose operator -(Pose a, Pose b)
    {
        return new Pose(a.Translation - b.Translation, a.Rotation - b.Rotation);
    }

    public static Pose operator +(Pose a, Vector2 translation)
    {
        return new Pose(a.Translation + translation.ToReal2<Displacement>(), a.Rotation);
    }

    public static Pose operator +(Pose a, Transformation b)
    {
        return a.Transformed(b);
    }

    public static Pose operator +(Pose a, Translation b)
    {
        return new Pose(a.Translation + b, a.Rotation);
    }

    public static Pose operator -(Pose a, Vector2 translation)
    {
        return new Pose(a.Translation - translation, a.Rotation);
    }

    public static Pose operator +(Pose a, Rotation rotation)
    {
        return new Pose(a.Translation, a.Rotation + rotation);
    }

    public static Pose operator -(Pose a, Rotation rotation)
    {
        return new Pose(a.Translation, a.Rotation - rotation);
    }

    #endregion

    public override int GetHashCode()
    {
        return HashCode.Combine(Translation, Rotation);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Pose other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Pose other)
    {
        return Translation.Equals(other.Translation) && Rotation.Equals(other.Rotation);
    }

    public override string ToString()
    {
        return $"Pose [{Translation}, {Rotation}]";
    }

    /// <summary>
    ///     Transforms the <see cref="Pose"/> by the <see cref="transformation"/>.
    /// </summary>
    /// <param name="transformation">The transformation to use.</param>
    /// <returns>A pose, transformed by the given <see cref="transformation"/>.</returns>
    public Pose Transformed(Transformation transformation)
    {
        return new Pose(
            Translation + transformation.Translation.Rotated(Rotation),
            transformation.Rotation + Rotation);
    }

    public static Transformation Transform(Pose a, Pose b)
    {
        return new Transformation(a, b);
    }

    public Pose RelativeTo(Pose other)
    {
        var transform = Transform(this, other);

        return new Pose(transform.Translation, transform.Rotation);
    }


    public Pose Exp(Twist twist)
    {
        var dx = twist.Dx;
        var dy = twist.Dy;
        var dTheta = twist.DTheta;

        var sinTheta = Math.Sin(dTheta);
        var cosTheta = Math.Cos(dTheta);
       
        double s;
        double c;
        
        if (Math.Abs(dTheta) < 1e-9)
        {
            s = 1.0 - 1.0 / 6.0 * dTheta * dTheta;
            c = 0.5 * dTheta;
        }
        else
        {
            s = sinTheta / dTheta;
            c = (1.0 - cosTheta) / dTheta;
        }

        var translation = new Translation(dx * s - dy * c, dx * c + dy * s);
        var rotation = Rotation.FromDirection(new Real2<Displacement>(cosTheta, sinTheta));

        var transform = new Transformation(translation, rotation);

        return this + transform;
    }

    public Twist Log(Pose end)
    {
        var transform = end.RelativeTo(this);
        var dTheta = transform.Rotation;
        var halfDTheta = dTheta / 2;

        var cosMinusOne = transform.Rotation.Cos - 1;

        double halfThetaByTanOfHalfDTheta;

        if (Math.Abs(cosMinusOne) < 1e-9)
        {
            halfThetaByTanOfHalfDTheta = 1.0 - 1.0 / 12.0 * dTheta * dTheta;
        }
        else
        {
            halfThetaByTanOfHalfDTheta = -(halfDTheta * transform.Rotation.Sin) / cosMinusOne;
        }

        var translationPart =
            transform.Translation.Rotated(Rotation.FromDirection(halfThetaByTanOfHalfDTheta, -halfDTheta))
            * Math.Sqrt(halfThetaByTanOfHalfDTheta * halfThetaByTanOfHalfDTheta + halfDTheta * halfDTheta);

        return new Twist(translationPart.X, translationPart.Y, dTheta);
    }

    public static Pose Interpolate(Pose a, Pose b, Real<Percentage> t)
    {
        if (t < 0)
        {
            return a;
        }

        if (t >= 1)
        {
            return b;
        }

        var twist = a.Log(b);

        return a.Exp(new Twist(
            twist.Dx * t.Value, 
            twist.Dy * t.Value, 
            twist.DTheta * t.Value));
    }

    public static Pose Lerp(Pose a, Pose b, Real<Percentage> t)
    {
        t = t.Clamped(0, 1);

        return new Pose(
            Real2<Displacement>.Lerp(a.Translation, b.Translation, t),
            Real<Angle>.Lerp(a.Rotation, b.Rotation, t));
    }
}