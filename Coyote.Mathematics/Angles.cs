namespace Coyote.Mathematics;

public static class Angles
{
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