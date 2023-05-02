using System.Numerics;

namespace Coyote.Mathematics;

public static class Extensions
{
    /// <summary>
    ///     Checks whether <see cref="f"/> is approximately equal to <see cref="other"/> using the specified <see cref="threshold"/>.
    /// </summary>
    /// <param name="f">The first value.</param>
    /// <param name="other">The second value.</param>
    /// <param name="threshold">A comparision threshold.</param>
    /// <returns>True, if the values are no more than <see cref="threshold"/> apart. Otherwise, false.</returns>
    public static bool ApproxEquals(this float f, float other, float threshold = 10e-6f)
    {
        return Math.Abs(f - other) < threshold;
    }

    /// <summary>
    ///     Checks whether <see cref="d"/> is approximately equal to <see cref="other"/> using the specified <see cref="threshold"/>.
    /// </summary>
    /// <param name="d">The first value.</param>
    /// <param name="other">The second value.</param>
    /// <param name="threshold">A comparision threshold.</param>
    /// <returns>True, if the values are no more than <see cref="threshold"/> apart. Otherwise, false.</returns>
    public static bool ApproxEquals(this double d, double other, double threshold = 10e-6)
    {
        return Math.Abs(d - other) < threshold;
    }

    public static Real<TUnit> ToReal<TUnit>(this float f)
    {
        return (Real<TUnit>)f;
    }

    public static Real<TUnit> ToReal<TUnit>(this double d)
    {
        return (Real<TUnit>)d;
    }

    public static Real2<TUnit> ToReal2<TUnit>(this Vector2 v)
    {
        return new Real2<TUnit>(new Real<TUnit>(v.X), new Real<TUnit>(v.Y));
    }

    public static Real<AngularDisplacement> ToRadians(this Real<AngleDegrees> r)
    {
        return new Real<AngularDisplacement>(Angles.ToRadians(r));
    }

    public static Real<AngleDegrees> ToDegrees(this Real<AngularDisplacement> r)
    {
        return new Real<AngleDegrees>(Angles.ToDegrees(r));
    }

    public static Real<TResult> Convert<TSource, TResult>(this Real<TSource> source)
    {
        return new Real<TResult>(source.Value);
    }

    public static Real2<TResult> Convert<TSource, TResult>(this Real2<TSource> source)
    {
        return new Real2<TResult>(source.X.Convert<TSource, TResult>(), source.Y.Convert<TSource, TResult>());
    }

    public static Rotation ToRotation(this Real2<Displacement> direction)
    {
        return Rotation.Exp(direction);
    }

    public static Rotation ToRotation(this Real2<Velocity> direction)
    {
        return direction.Convert<Velocity, Displacement>().ToRotation();
    }

    public static Rotation ToRotation(this Real2<Acceleration> direction)
    {
        return direction.Convert<Acceleration, Displacement>().ToRotation();
    }

    public static Real<Displacement> Integrate(this Real<Velocity> velocity, Real<Time> time)
    {
        return new Real<Displacement>(velocity.Value * time.Value);
    }

    public static Real<Velocity> Integrate(this Real<Acceleration> acceleration, Real<Time> time)
    {
        return new Real<Velocity>(acceleration.Value * time.Value);
    }
    
    public static Real<T> Squared<T>(this Real<T> real)
    {
        return new Real<T>(real.Value * real.Value);
    }

    public static Real<T> SquareRoot<T>(this Real<T> real)
    {
        return new Real<T>(Math.Sqrt(real.Value));
    }

    public static bool IsNan(this double d)
    {
        return double.IsNaN(d);
    }

    public static Real2<TUnit> ToReal2<TUnit>(this RealVector<TUnit> vector)
    {
        Vectors.Validate(vector, 2);

        return new Real2<TUnit>(vector[0], vector[1]);
    }

    public static RealVector<TUnit> ToRealVector<TUnit>(this Real<TUnit> real)
    {
        return new RealVector<TUnit>(real);
    }

    public static RealVector<TUnit> ToRealVector<TUnit>(this Real2<TUnit> real)
    {
        return new RealVector<TUnit>(real.X, real.Y);
    }

    public static RealVector<TUnit> ToRealVector<TUnit>(this Vector2 v)
    {
        return new RealVector<TUnit>(new Real<TUnit>(v.X), new Real<TUnit>(v.Y));
    }

    public static Vector2 ToVector2<TUnit>(this RealVector<TUnit> vector)
    {
        Vectors.Validate(vector, 2);

        return new Vector2((float)vector[0], (float)vector[1]);
    }

    public static RealVector<TUnit> ToRealVector<TUnit>(this double d)
    {
        return new RealVector<TUnit>(new Real<TUnit>(d));
    }
}