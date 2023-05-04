using System.Numerics;

namespace Coyote.Mathematics;

// We could have also used System.Numerics but I want doubles everywhere.
// Also, Vector2 would conflict with System.Numerics, and Vector2D would not play well with Vector2DDual.
// Translation2 doesn't look particularly enticing (even though we'd probably end up with some Rotation2d class)

public readonly struct Vector2d
{
    public static readonly Vector2d Zero = new(0, 0);
    public static readonly Vector2d One = new(1, 1);
    public static readonly Vector2d UnitX = new(1, 0);
    public static readonly Vector2d UnitY = new(0, 1);

    public double X { get; }
    public double Y { get; }

    public Vector2d(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Vector2d(double value) : this(value, value)
    {

    }

    // We may consider using functions here. Normalized() is already using it.
    public double LengthSqr => X * X + Y * Y;
    public double Length => Math.Sqrt(LengthSqr);
    public Vector2d Normalized() => this / Length;

    public override bool Equals(object? obj)
    {
        if (obj is not Vector2d other)
        {
            return false;
        }

        return Equals(other);
    }
    
    public bool Equals(Vector2d other)
    {
        return X.Equals(other.X) &&
               Y.Equals(other.Y);
    }

    public bool ApproxEqs(Vector2d other, double eps = 10e-6)
    {
        return X.ApproxEqs(other.X, eps) && 
               Y.ApproxEqs(other.Y, eps);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"X={X}, Y={Y}";
    }

    public static bool operator ==(Vector2d a, Vector2d b) => a.Equals(b);
    public static bool operator !=(Vector2d a, Vector2d b) => !a.Equals(b);
    public static Vector2d operator +(Vector2d v) => new(+v.X, +v.Y);
    public static Vector2d operator -(Vector2d v) => new(-v.X, -v.Y);
    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator *(Vector2d a, Vector2d b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2d operator /(Vector2d a, Vector2d b) => new(a.X / b.X, a.Y / b.Y);
    public static Vector2d operator *(Vector2d v, double scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2d operator /(Vector2d v, double scalar) => new(v.X / scalar, v.Y / scalar);

    public static implicit operator Vector2(Vector2d v) => new((float)v.X, (float)v.Y);
    public static implicit operator Vector2d(Vector2 v) => new(v.X, v.Y);

    public static Vector2d Lerp(Vector2d a, Vector2d b, double t)
    {
        return new Vector2d(
            MathExt.Lerp(a.X, b.X, t),
            MathExt.Lerp(a.Y, b.Y, t)
        );
    }
}

public sealed class Vector2dDual
{
    public Dual X { get; }
    public Dual Y { get; }

    public Vector2dDual(Dual x, Dual y)
    {
        if (x.Size != y.Size)
        {
            throw new ArgumentException("X and Y duals are not of the same size");
        }

        if (x.Size == 0)
        {
            throw new ArgumentException($"Cannot create {nameof(Vector2dDual)} with empty components.");
        }

        X = x;
        Y = y;
    }

    public Vector2dDual(Dual value) : this(value, value)
    {

    }

    public int Size => X.Size;
    public bool IsReal => Size == 1;

    // We may consider using functions here. Normalized() is already using it.
    public Dual LengthSqr => X * X + Y * Y;
    public Dual Length => Dual.Sqrt(LengthSqr);
    public Vector2dDual Normalized() => this / Length;
    public Vector2d Value => new(X.Value, Y.Value);
    public Vector2dDual Head(int n = 1) => new(X.Head(n), Y.Head(n));
    public Vector2dDual Tail(int n = 1) => new(X.Tail(n), Y.Tail(n));

    public override bool Equals(object? obj)
    {
        if (obj is not Vector2dDual other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Vector2dDual other)
    {
        return X.Equals(other.X) &&
               Y.Equals(other.Y);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"X={X}, Y={Y}";
    }

    public static bool operator ==(Vector2dDual a, Vector2dDual b) => a.Equals(b);
    public static bool operator !=(Vector2dDual a, Vector2dDual b) => !a.Equals(b);
    public static Vector2dDual operator +(Vector2dDual v) => new(+v.X, +v.Y);
    public static Vector2dDual operator -(Vector2dDual v) => new(-v.X, -v.Y);
    public static Vector2dDual operator +(Vector2dDual a, Vector2dDual b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2dDual operator -(Vector2dDual a, Vector2dDual b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2dDual operator *(Vector2dDual a, Vector2dDual b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2dDual operator /(Vector2dDual a, Vector2dDual b) => new(a.X / b.X, a.Y / b.Y);
    public static Vector2dDual operator *(Vector2dDual v, Dual scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2dDual operator /(Vector2dDual v, Dual scalar) => new(v.X / scalar, v.Y / scalar);
    public static Vector2dDual operator *(Vector2dDual v, double constant) => new(v.X * constant, v.Y * constant);
    public static Vector2dDual operator /(Vector2dDual v, double constant) => new(v.X / constant, v.Y / constant);

    public static Vector2dDual Const(double x, double y, int n = 1) => new(Dual.Const(x, n), Dual.Const(y, n));
    public static Vector2dDual Const(Vector2d v, int n = 1) => Const(v.X, v.Y, n);
    public static Vector2dDual Var(double x, double y, int n = 1) => new(Dual.Var(x, n), Dual.Var(y, n));
}

public readonly struct Rotation2d
{
    public static readonly Rotation2d Zero = Exp(0.0);

    public double Re { get; }
    public double Im { get; }

    public Rotation2d(double re, double im)
    {
        Re = re;
        Im = im;
    }

    public static Rotation2d Exp(double angleIncr) => new(Math.Cos(angleIncr), Math.Sin(angleIncr));

    public static Rotation2d Dir(Vector2d direction)
    {
        direction = direction.Normalized();

        return new Rotation2d(direction.X, direction.Y);
    }

    public double Log() => Math.Atan2(Im, Re);
    public Rotation2d Scaled(double k) => Exp(Log() * k);
    public Rotation2d Inverse => new(Re, -Im);
    public Vector2d Direction => new(Re, Im);

    public override bool Equals(object? obj)
    {
        if (obj is not Rotation2d other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Rotation2d other)
    {
        return other.Re.Equals(Re) && 
               other.Im.Equals(Im);
    }

    public bool ApproxEqs(Rotation2d other, double eps = 10e-10)
    {
        return Re.ApproxEqs(other.Re, eps) && 
               Im.ApproxEqs(other.Im, eps);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Re, Im);
    }

    public override string ToString()
    {
        // Would this cause any trouble? :

        return $"Angle={Angles.ToDegrees(Log())} deg";
    }

    public static bool operator ==(Rotation2d a, Rotation2d b) => a.Equals(b);
    public static bool operator !=(Rotation2d a, Rotation2d b) => !a.Equals(b);
    public static Rotation2d operator *(Rotation2d a, Rotation2d b) => new(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);
    public static Vector2d operator *(Rotation2d a, Vector2d r2) => new(a.Re * r2.X - a.Im * r2.Y, a.Im * r2.X + a.Re * r2.Y);
    public static Rotation2d operator /(Rotation2d a, Rotation2d b) => b.Inverse * a;

    public static Rotation2d Interpolate(Rotation2d r0, Rotation2d r1, double t)
    {
        return Exp(t * (r1 / r0).Log()) * r0;
    }
}

public sealed class Rotation2dDual
{
    public Dual Re { get; }
    public Dual Im { get; }

    public Rotation2dDual(Dual re, Dual im)
    {
        Re = re;
        Im = im;
    }

    public static Rotation2dDual Const(Rotation2d value, int n = 1) => new(Dual.Const(value.Re, n), Dual.Const(value.Im, n));
    public static Rotation2dDual Const(double angleIncr, int n = 1) => Exp(Dual.Const(angleIncr, n));

    public static Rotation2dDual Exp(Dual angleIncr) => new(Dual.Cos(angleIncr), Dual.Sin(angleIncr));

    public Rotation2d Value => new(Re.Value, Im.Value);
    public Dual AngularVelocity => Re * Im.Tail() - Im * Re.Tail();
    public Rotation2dDual Inverse => new(Re, -Im);
    public Vector2dDual Direction => new(Re, Im);

    public override bool Equals(object? obj)
    {
        if (obj is not Rotation2dDual other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Rotation2dDual other)
    {
        return other.Re.Equals(Re) &&
               other.Im.Equals(Im);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Re, Im);
    }

    public static bool operator ==(Rotation2dDual a, Rotation2dDual b) => a.Equals(b);
    public static bool operator !=(Rotation2dDual a, Rotation2dDual b) => !a.Equals(b);
    public static Rotation2dDual operator *(Rotation2dDual a, Rotation2dDual b) => new(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);
    public static Rotation2dDual operator *(Rotation2dDual a, Rotation2d b) => new(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);
    public static Vector2dDual operator *(Rotation2dDual a, Vector2dDual r2) => new(a.Re * r2.X - a.Im * r2.Y, a.Im * r2.X + a.Re * r2.Y);
    public static Vector2dDual operator *(Rotation2dDual a, Vector2d r2) => new(a.Re * r2.X - a.Im * r2.Y, a.Im * r2.X + a.Re * r2.Y);
}

public readonly struct Twist2dIncr
{
    public Vector2d TrIncr { get; }
    public double RotIncr { get; }

    public Twist2dIncr(Vector2d tTrIncr, double rotIncr)
    {
        TrIncr = tTrIncr;
        RotIncr = rotIncr;
    }

    public Twist2dIncr(double xIncr, double yIncr, double rIncr) : this(new Vector2d(xIncr, yIncr), rIncr)
    {

    }

    public override bool Equals(object? obj)
    {
        if (obj is not Twist2dIncr other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Twist2dIncr other)
    {
        return TrIncr.Equals(other.TrIncr) &&
               RotIncr.Equals(other.RotIncr);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TrIncr, RotIncr);
    }

    public override string ToString()
    {
        return $"{TrIncr} {RotIncr}";
    }

    public static bool operator ==(Twist2dIncr a, Twist2dIncr b) => a.Equals(b);
    public static bool operator !=(Twist2dIncr a, Twist2dIncr b) => !a.Equals(b);
}

public sealed class Twist2dIncrDual
{
    public Vector2dDual TrIncr { get; }
    public Dual RotIncr { get; }

    public Twist2dIncrDual(Vector2dDual tTrIncr, Dual rotIncr)
    {
        TrIncr = tTrIncr;
        RotIncr = rotIncr;
    }

    public static Twist2dIncrDual Const(Vector2d trIncr, double rotIncr, int n = 1) => new(Vector2dDual.Const(trIncr, n), Dual.Const(rotIncr, n));

    public Twist2dIncr Value => new(TrIncr.Value, RotIncr.Value);
    public Twist2dDual Velocity => new(TrIncr.Tail(), RotIncr.Tail());

    public override bool Equals(object? obj)
    {
        if (obj is not Twist2dIncrDual other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Twist2dIncrDual other)
    {
        return TrIncr.Equals(other.TrIncr) &&
               RotIncr.Equals(other.RotIncr);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TrIncr, RotIncr);
    }

    public override string ToString()
    {
        return $"{TrIncr} {RotIncr}";
    }
}

public readonly struct Twist2d
{
    public static readonly Twist2d Zero = new(0, 0, 0);

    public Vector2d TrVelocity { get; }
    public double RotVelocity { get; }

    public Twist2d(Vector2d trVelocity, double rotVelocity)
    {
        TrVelocity = trVelocity;
        RotVelocity = rotVelocity;
    }

    public Twist2d(double dx, double dy, double dTheta) : this(new Vector2d(dx, dy), dTheta)
    {

    }

    public override bool Equals(object? obj)
    {
        if (obj is not Twist2d other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Twist2d other)
    {
        return TrVelocity.Equals(other.TrVelocity) &&
               RotVelocity.Equals(other.RotVelocity);
    }

    public bool ApproxEqs(Twist2d other, double eps = 10e-10)
    {
        return TrVelocity.ApproxEqs(other.TrVelocity, eps) &&
               RotVelocity.ApproxEqs(other.RotVelocity, eps);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TrVelocity, RotVelocity);
    }

    public override string ToString()
    {
        return $"{TrVelocity} {RotVelocity}";
    }

    public static bool operator ==(Twist2d a, Twist2d b) => a.Equals(b);
    public static bool operator !=(Twist2d a, Twist2d b) => !a.Equals(b);
    public static Twist2d operator +(Twist2d a, Twist2d b) => new(a.TrVelocity + b.TrVelocity, a.RotVelocity + b.RotVelocity);
    public static Twist2d operator -(Twist2d a, Twist2d b) => new(a.TrVelocity - b.TrVelocity, a.RotVelocity - b.RotVelocity);
    public static Twist2d operator *(Twist2d tw, double scalar) => new(tw.TrVelocity * scalar, tw.RotVelocity * scalar);
    public static Twist2d operator /(Twist2d tw, double scalar) => new(tw.TrVelocity / scalar, tw.RotVelocity / scalar);
}

public sealed class Twist2dDual
{
    public Vector2dDual TrVelocity { get; }
    public Dual RotVelocity { get; }

    public Twist2dDual(Vector2dDual trVelocity, Dual rotVelocity)
    {
        TrVelocity = trVelocity;
        RotVelocity = rotVelocity;
    }

    public static Twist2dDual Const(Twist2d value, int n = 1) => new(Vector2dDual.Const(value.TrVelocity, n), Dual.Const(value.RotVelocity, n));

    public Twist2d Value => new(TrVelocity.Value, RotVelocity.Value);

    public override bool Equals(object? obj)
    {
        if (obj is not Twist2dDual other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Twist2dDual other)
    {
        return TrVelocity.Equals(other.TrVelocity) &&
               RotVelocity.Equals(other.RotVelocity);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TrVelocity, RotVelocity);
    }

    public override string ToString()
    {
        return $"{TrVelocity} {RotVelocity}";
    }

    public static bool operator ==(Twist2dDual a, Twist2dDual b) => a.Equals(b);
    public static bool operator !=(Twist2dDual a, Twist2dDual b) => !a.Equals(b);
    public static Twist2dDual operator +(Twist2dDual a, Twist2dDual b) => new(a.TrVelocity + b.TrVelocity, a.RotVelocity + b.RotVelocity);
    public static Twist2dDual operator -(Twist2dDual a, Twist2dDual b) => new(a.TrVelocity - b.TrVelocity, a.RotVelocity - b.RotVelocity);
}

public readonly struct Pose2d
{
    public Vector2d Translation { get; }
    public Rotation2d Rotation { get; }

    public Pose2d(Vector2d translation, Rotation2d rotation)
    {
        Translation = translation;
        Rotation = rotation;
    }

    public Pose2d(double x, double y, double r) : this(new Vector2d(x, y), Rotation2d.Exp(r))
    {

    }

    public Pose2d Inverse => new(Rotation.Inverse * -Translation, Rotation.Inverse);

    public static Pose2d Exp(Twist2dIncr incr)
    {
        var rot = Rotation2d.Exp(incr.RotIncr);

        var u = incr.RotIncr + MathExt.SnzEps(incr.RotIncr);
        var c = 1.0 - Math.Cos(u);
        var s = Math.Sin(u);

        return new Pose2d(
            new Vector2d(
                (s * incr.TrIncr.X - c * incr.TrIncr.Y) / u,
                (c * incr.TrIncr.X + s * incr.TrIncr.Y) / u
            ),
            rot);
    }

    public Twist2dIncr Log()
    {
        var angle = Rotation.Log();

        var u2 = 0.5 * angle + MathExt.SnzEps(angle);
        var halfTan = u2 / Math.Tan(u2);

        return new Twist2dIncr(
            new Vector2d(
                halfTan * Translation.X + u2 * Translation.Y,
                -u2 * Translation.X + halfTan * Translation.Y
            ),
            angle
        );
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Pose2d other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Pose2d other)
    {
        return Translation.Equals(other.Translation) &&
               Rotation.Equals(other.Rotation);
    }

    public bool ApproxEqs(Pose2d other, double eps = 10e-10)
    {
        return Translation.ApproxEqs(other.Translation, eps) && 
               Rotation.ApproxEqs(other.Rotation, eps);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Translation, Rotation);
    }

    public override string ToString()
    {
        return $"{Translation} {Rotation}";
    }

    public static bool operator ==(Pose2d a, Pose2d b) => a.Equals(b);
    public static bool operator !=(Pose2d a, Pose2d b) => !a.Equals(b);
    public static Pose2d operator *(Pose2d a, Pose2d b) => new(a.Translation + a.Rotation * b.Translation, a.Rotation * b.Rotation);
    public static Vector2d operator *(Pose2d a, Vector2d v) => a.Translation + a.Rotation * v;
    public static Pose2d operator /(Pose2d a, Pose2d b) => b.Inverse * a;
    public static Pose2d operator +(Pose2d p, Twist2dIncr incr) => p * Exp(incr);
    public static Twist2dIncr operator -(Pose2d a, Pose2d b) => (a / b).Log();
}

public sealed class Pose2dDual
{
    public Vector2dDual Translation { get; }
    public Rotation2dDual Rotation { get; }

    public Pose2dDual(Vector2dDual translation, Rotation2dDual rotation)
    {
        Translation = translation;
        Rotation = rotation;
    }

    public Pose2dDual Inverse => new(Rotation.Inverse * -Translation, Rotation.Inverse);
    public Pose2d Value => new(Translation.Value, Rotation.Value);
    public Twist2dDual Velocity => new(Translation.Tail(), Rotation.AngularVelocity);

    public override bool Equals(object? obj)
    {
        if (obj is not Pose2dDual other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(Pose2dDual other)
    {
        return Translation.Equals(other.Translation) &&
               Rotation.Equals(other.Rotation);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Translation, Rotation);
    }

    public override string ToString()
    {
        return $"{Translation} {Rotation}";
    }

    public static bool operator ==(Pose2dDual a, Pose2dDual b) => a.Equals(b);
    public static bool operator !=(Pose2dDual a, Pose2dDual b) => !a.Equals(b);
    public static Pose2dDual operator *(Pose2dDual a, Pose2dDual b) => new(a.Translation + a.Rotation * b.Translation, a.Rotation * b.Rotation);
    public static Pose2dDual operator *(Pose2dDual a, Pose2d b) => new(a.Translation + a.Rotation * b.Translation, a.Rotation * b.Rotation);
    public static Pose2dDual operator /(Pose2dDual a, Pose2dDual b) => b.Inverse * a;
    public static Pose2dDual operator +(Pose2dDual p, Twist2dIncr incr) => p * Pose2d.Exp(incr);
}