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

    public static Real<TUnit> ToReal<TUnit>(this float f) where TUnit : IUnit
    {
        return (Real<TUnit>)f;
    }

    public static Real<TUnit> ToReal<TUnit>(this double d) where TUnit : IUnit
    {
        return (Real<TUnit>)d;
    }

    public static Real2<TUnit> ToReal2<TUnit>(this Vector2 v) where TUnit : IUnit
    {
        return (Real2<TUnit>)v;
    }

    public static Real<Radians> ToRadians(this Real<Degrees> r)
    {
        return new Real<Radians>(Angles.ToRadians(r));
    }

    public static Real<Degrees> ToDegrees(this Real<Radians> r)
    {
        return new Real<Degrees>(Angles.ToDegrees(r));
    }

    public static Real<TResult> Convert<TSource, TResult>(this Real<TSource> source)
        where TSource : IUnit
        where TResult : IUnit
    {
        return new Real<TResult>(source.Value);
    }

    public static Real2<TResult> Convert<TSource, TResult>(this Real2<TSource> source)
        where TResult : IUnit
        where TSource : IUnit
    {
        return new Real2<TResult>(source.X.Convert<TSource, TResult>(), source.Y.Convert<TSource, TResult>());
    }

    public static Rotation ToRotation(this Real2<Displacement> direction)
    {
        return Rotation.FromDirection(direction);
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

    public static Real<T> Squared<T>(this Real<T> real) where T : IUnit
    {
        return new Real<T>(real.Value * real.Value);
    }

    public static Real<T> SquareRoot<T>(this Real<T> real) where T : IUnit
    {
        return new Real<T>(Math.Sqrt(real.Value));
    }
}