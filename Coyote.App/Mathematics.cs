namespace Coyote.App;

internal static class Mathematics
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