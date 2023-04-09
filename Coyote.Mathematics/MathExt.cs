using System.Numerics;

namespace Coyote.Mathematics;

internal class MathExt
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
}