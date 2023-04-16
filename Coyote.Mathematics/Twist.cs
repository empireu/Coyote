using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

public readonly struct Twist
{
    [JsonInclude]
    public Real<Displacement> Dx { get; }

    [JsonInclude]
    public Real<Displacement> Dy { get; }

    [JsonInclude]
    public Real<AngularDisplacement> DTheta { get; }

    [JsonConstructor]
    public Twist(Real<Displacement> dx, Real<Displacement> dy, Real<AngularDisplacement> dTheta)
    {
        Dx = dx;
        Dy = dy;
        DTheta = dTheta;
    }

    public Twist(double dx, double dy, double dTheta) : this(dx.ToReal<Displacement>(), dy.ToReal<Displacement>(), dTheta.ToReal<AngularDisplacement>())
    {

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

    public bool EqualsApprox(Twist other, double tolerance = 10e-6f)
    {
        return Dx.ApproxEquals(other.Dx, tolerance) && 
               Dy.ApproxEquals(other.Dy, tolerance) && 
               DTheta.ApproxEquals(other.DTheta, tolerance);
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