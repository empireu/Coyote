using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

public readonly struct Twist
{
    [JsonInclude]
    public double Dx { get; }

    [JsonInclude]
    public double Dy { get; }

    [JsonInclude]
    public double DTheta { get; }

    [JsonConstructor]
    public Twist(double dx, double dy, double dTheta)
    {
        Dx = dx;
        Dy = dy;
        DTheta = dTheta;
    }

    public override string ToString()
    {
        return $"Twist [Dx={Dx}, Dy={Dy}, DTheta={DTheta}]";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Twist other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Twist other)
    {
        return Dx.Equals(other.Dx) && Dy.Equals(other.Dy) && DTheta.Equals(other.DTheta);
    }

    public static bool operator ==(Twist a, Twist b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Twist a, Twist b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Dx, Dy, DTheta);
    }
}