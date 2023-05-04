namespace Coyote.Tests;

internal static class Utilities
{
    public static void RangeScan(Action<double> action, double start = 0d, double end = 10d, int steps = 10000)
    {
        var stepSize = (end - start) / steps;

        var x = start;

        while (x < end)
        {
            action(x);
            x += stepSize;
        }
    }

    public static void RangeScanRec(Action<double[]> action, int layers, double start = 0d, double end = 10d, int steps = 10)
    {
        void Helper(int depth, double[] results)
        {
            RangeScan(v =>
            {
                results[depth] = v;

                if (depth > 0)
                {
                    Helper(depth - 1, results);
                }
                else
                {
                    action(results);
                }

            }, start: start, end: end, steps: steps);
        }

        var results = new double[layers];

        Helper(layers - 1, results);
    }
}