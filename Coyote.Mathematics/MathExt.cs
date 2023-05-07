using System.Numerics;
using System.Runtime.CompilerServices;

namespace Coyote.Mathematics;

public static class MathExt
{
    public static T Min<T>(params T[] values) where T : IComparisonOperators<T, T, bool>
    {
        var min = values[0];

        if (values.Length == 1)
        {
            return min;
        }

        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] < min)
            {
                min = values[i];
            }
        }

        return min;
    }

    public static T Max<T>(params T[] values) where T : IComparisonOperators<T, T, bool>
    {
        var max = values[0];
        
        if (values.Length == 0)
        {
            return max;
        }

        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }

        return max;
    }

    public static double Lerp(double value1, double value2, double amount)
    {
        return value1 * (1.0 - amount) + value2 * amount;
    }

    public static double MinNaN(double a, double b)
    {
        if (double.IsNaN(a) && double.IsNaN(b))
        {
            throw new ArgumentException("Both A and B were NaN.");
        }

        if (double.IsNaN(a))
        {
            return b;
        }

        if (double.IsNaN(b))
        {
            return a;
        }

        return Math.Min(a, b);
    }

    public static double MaxNaN(double a, double b)
    {
        if (double.IsNaN(a) && double.IsNaN(b))
        {
            throw new ArgumentException("Both A and B were NaN.");
        }

        if (double.IsNaN(a))
        {
            return b;
        }

        if (double.IsNaN(b))
        {
            return a;
        }

        return Math.Max(a, b);
    }

    // Sign-non-zero function from the SymForce paper.
    // Roadrunner uses this instead of the approximation method for sin(x)/x,
    // And it is cleaner in my opinion.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SignNonZero(double a)
    {
        if (a >= 0.0)
        {
            return 1;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SnzEps(double a)
    {
        if (a >= 0.0)
        {
            return 2.2e-15;
        }

        return -2.2e-15;
    }
}