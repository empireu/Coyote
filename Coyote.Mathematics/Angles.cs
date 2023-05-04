namespace Coyote.Mathematics;

public static class Angles
{
    /// <summary>
    ///     Computes the module of the angle <see cref="a"/> in the specified interval <see cref="n"/>
    /// </summary>
    /// <param name="a">The angle to use.</param>
    /// <param name="n">The modulus interval to use.</param>
    /// <returns>An angle, wrapped in the interval <see cref="n"/></returns>
    public static double Modulus(double a, double n)
    {
        return (a % n + n) % n;
    }

    /// <summary>
    ///     Computes the module of the angle <see cref="a"/> in the specified interval <see cref="n"/>
    /// </summary>
    /// <param name="a">The angle to use.</param>
    /// <param name="n">The modulus interval to use.</param>
    /// <returns>An angle, wrapped in the interval <see cref="n"/></returns>
    public static Rotation Modulus(Rotation a, Rotation n)
    {
        return new Rotation(Modulus(a.Angle, n.Angle));
    }

    /// <summary>
    ///     Gets the smallest angle between <see cref="a"/> and <see cref="b"/>.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static double DeltaAngle(double a, double b)
    {
        return Modulus(a - b + Math.PI, Math.Tau) - Math.PI;
    }

    /// <summary>
    ///     Gets the smallest angle between <see cref="a"/> and <see cref="b"/>.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static Rotation DeltaAngle(Rotation a, Rotation b)
    {
        return (Rotation)(Modulus(a - b + Math.PI, Math.Tau) - Math.PI);
    }

    public static double ToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }

    public static float ToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }

    public static double ToDegrees(double radians)
    {
        return radians * (180.0 / Math.PI);
    }

    public static float ToDegrees(float radians)
    {
        return radians * (180f / MathF.PI);
    }
}