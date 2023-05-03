using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace Coyote.Mathematics;

/// <summary>
///     Generalized <see href="https://en.wikipedia.org/wiki/Dual_number">Dual Number</see>
///     using the algorithm described in <see href="https://pp.bme.hu/eecs/article/view/16341">Higher Order Automatic Differentiation with Dual Numbers</see>
/// </summary>
public sealed class Dual
{
    private readonly double[] _values;

    public double this[int index] => _values[index];
    public int Size => _values.Length;
    public bool IsReal => Size == 1;
    public double Value => _values[0];

    private Dual(double[] values)
    {
        _values = values;
    }

    public Dual(double value, Dual tail)
    {
        _values = new double[1 + tail.Size];

        _values[0] = value;

        for (var i = 0; i < tail.Size; i++)
        {
            _values[i + 1] = tail[i];
        }
    }

    /// <summary>
    ///     Gets the values at the start of the <see cref="Dual"/>, ignoring the last <see cref="n"/> values.
    /// </summary>
    public Dual Head(int n = 1) => new(_values.AsSpan(0, Size - n).ToArray());

    /// <summary>
    ///     Gets the values at the end of the <see cref="Dual"/>, ignoring the first <see cref="n"/> values.
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public Dual Tail(int n = 1) => new(_values.AsSpan(n, Size - n).ToArray());

    public override bool Equals(object? obj)
    {
        if (obj is not Dual other)
        {
            return false;
        }

        return Equals(other);
    }
    
    public bool Equals(Dual other)
    {
        return _values.SequenceEqual(other._values);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        for (var i = 0; i < _values.Length; i++)
        {
            hashCode.Add(_values[i]);
        }

        return hashCode.ToHashCode();
    }

    public override string ToString()
    {
        return string.Join(", ", _values);
    }

    public double[] ToArray()
    {
        return _values.ToArray();
    }

    public static bool operator ==(Dual a, Dual b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Dual a, Dual b)
    {
        return !a.Equals(b);
    }

    public static Dual operator +(Dual d)
    {
        return d;
    }

    public static Dual operator -(Dual d)
    {
        var values = new double[d.Size];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = -d[i];
        }

        return new Dual(values);
    }

    public static Dual operator +(Dual d, double constant)
    {
        var values = d.ToArray();
        values[0] += constant;

        return new Dual(values);
    }

    public static Dual operator -(Dual d, double constant)
    {
        var values = d.ToArray();
        values[0] -= constant;

        return new Dual(values);
    }

    public static Dual operator *(Dual d, double constant)
    {
        var values = d.ToArray();

        for (var i = 0; i < values.Length; i++)
        {
            values[i] *= constant;
        }

        return new Dual(values);
    }

    public static Dual operator /(Dual d, double constant)
    {
        var values = d.ToArray();

        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= constant;
        }

        return new Dual(values);
    }

    // P.S. all of these can be optimized but there's no need for that yet.

    public static Dual operator +(double constant, Dual d) => Const(constant, d.Size) + d;
    public static Dual operator -(double constant, Dual d) => Const(constant, d.Size) - d;
    public static Dual operator *(double constant, Dual d) => Const(constant, d.Size) * d;
    public static Dual operator /(double constant, Dual d) => Const(constant, d.Size) / d;

    public static Dual operator +(Dual a, Dual b) => a.IsReal || b.IsReal ? Const(a.Value + b.Value) : new Dual(a.Value + b.Value, a.Tail() + b.Tail());
    public static Dual operator -(Dual a, Dual b) => a.IsReal || b.IsReal ? Const(a.Value - b.Value) : new Dual(a.Value - b.Value, a.Tail() - b.Tail());
    public static Dual operator *(Dual a, Dual b) => a.IsReal || b.IsReal ? Const(a.Value * b.Value) : new Dual(a.Value * b.Value, a.Tail() * b.Head() + a.Head() * b.Tail());
    public static Dual operator /(Dual a, Dual b) => a.IsReal || b.IsReal ? Const(a.Value / b.Value) : new Dual(a.Value / b.Value, (a.Tail() * b - a * b.Tail()) / (b * b));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Dual Const(double value, int n = 1)
    {
        var values = new double[n];
        values[0] = value;

        return new Dual(values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Dual Var(double value, int n = 1)
    {
        var values = new double[n];

        values[0] = value;
        values[1] = 1;

        return new Dual(values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Dual Function(Func<double, double> x, Func<Dual, Dual> dxFront)
    {
        return IsReal ? Const(x(Value)) : new Dual(x(Value), dxFront(Head()) * Tail());
    }

    public Dual Sqr() => this * this;

    public static Dual Sin(Dual d) => d.Function(Math.Sin, Cos);
    public static Dual Cos(Dual d) => d.Function(Math.Cos, h => -Sin(h));
    public static Dual Tan(Dual d) => d.Function(Math.Tan, h => (1.0 / Cos(h)).Sqr());
    public static Dual Atan(Dual d) => d.Function(Math.Atan, h => 1 / (h * h + 1.0));
    public static Dual Log(Dual d) => d.Function(Math.Log, h => 1.0 / h);
    public static Dual Pow(Dual d, double n) => d.Function(x => Math.Pow(x, n), h => n * Pow(h, n - 1.0));
    public static Dual Sqrt(Dual d) => d.Function(Math.Sqrt, h => (Const(1.0, d.Size) / (Const(2.0, d.Size) * Sqrt(h))));
}