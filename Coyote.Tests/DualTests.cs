using Coyote.Mathematics;

namespace Coyote.Tests;

public class DualTests
{
    private const double Eps = 10e-12;

    private static void RangeScan(Action<double> action, double start = 0d, double end = 10d, int steps = 10000)
    {
        var stepSize = (end - start) / steps;

        var x = start;

        while (x < end)
        {
            action(x);
            x += stepSize;
        }
    }

    private static void RangeScan(Action<double, Dual> action, int derivatives = 3, double start = 0d, double end = 10d, int steps = 10000)
    {
        RangeScan(x =>
        {
            action(x, Dual.Var(x, derivatives + 1));
        }, start, end, steps);
    }

    private static void RangeScanRec(Action<double[]> action, int layers, double start = 0d, double end = 10d, int steps = 10)
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

    private static void AreEqual(double a, double b)
    {
        Assert.That(a, Is.EqualTo(b).Within(Eps));
    }

    [Test]
    public void SqrtTest()
    {
        RangeScan((x, xDual) =>
        {
            var sqrt = Dual.Sqrt(xDual);

            AreEqual(sqrt.Value, Math.Sqrt(x));
            AreEqual(sqrt[1], 1.0 / (2 * Math.Sqrt(x)));
            AreEqual(sqrt[2], -1.0 / (4.0 * Math.Pow(x, 3.0 / 2.0)));
            AreEqual(sqrt[3], 3.0 / (8.0 * Math.Pow(x, 5.0 / 2.0)));
        }, start: 1);       
    }

    [Test]
    public void SinTest()
    {
        RangeScan((x, xDual) =>
        {
            var sin = Dual.Sin(xDual);

            AreEqual(sin.Value, Math.Sin(x));
            AreEqual(sin[1], Math.Cos(x));
            AreEqual(sin[2], -Math.Sin(x));
            AreEqual(sin[3], -Math.Cos(x));
        }, start: 1);
    }

    [Test]
    public void TanTest()
    {
        double Sec(double v)
        {
            return 1d / Math.Cos(v);
        }

        RangeScan((x, xDual) =>
        {
            var tan = Dual.Tan(xDual);

            AreEqual(tan.Value, Math.Tan(x));
            AreEqual(tan[1], Sec(x) * Sec(x));
            AreEqual(tan[2], 2 * Math.Tan(x) * Sec(x) * Sec(x));
            AreEqual(tan[3], 2 * Sec(x) * Sec(x) * (2 * Math.Tan(x) * Math.Tan(x) + Sec(x) * Sec(x)));
        }, start: 0, end: 1);
    }

    [Test]
    public void AtanTest()
    {
        RangeScan((x, xDual) =>
        {
            var atan = Dual.Atan(xDual);

            AreEqual(atan.Value, Math.Atan(x));
            AreEqual(atan[1], 1 / (x * x + 1));
            AreEqual(atan[2], -(2 * x) / ((x * x + 1) * (x * x + 1)));
            AreEqual(atan[3], (6 * x * x - 2) / ((x * x + 1) * (x * x + 1) * (x * x + 1)));
        }, start: -10, end: 10);
    }

    [Test]
    public void CosTest()
    {
        RangeScan((x, xDual) =>
        {
            var cos = Dual.Cos(xDual);

            AreEqual(cos.Value, Math.Cos(x));
            AreEqual(cos[1], -Math.Sin(x));
            AreEqual(cos[2], -Math.Cos(x));
            AreEqual(cos[3], Math.Sin(x));
        }, start: 1, end: 100);
    }

    [Test]
    public void LogTest()
    {
        RangeScan((x, xDual) =>
        {
            var log = Dual.Log(xDual);

            AreEqual(log.Value, Math.Log(x));
            AreEqual(log[1], 1.0 / x);
            AreEqual(log[2], -1.0 / (x * x));
            AreEqual(log[3], 2.0 / (x * x * x));
        }, start: 1, end: 100.0);
    }

    [Test]
    public void PowTest()
    {
        RangeScan(power =>
        {
            RangeScan((x, xDual) =>
            {
                var pow = Dual.Pow(xDual, power);

                AreEqual(pow.Value, Math.Pow(x, power));
                AreEqual(pow[1], power * Math.Pow(x, power - 1));
                AreEqual(pow[2], (power - 1) * power * Math.Pow(x, power - 2));
                AreEqual(pow[3], (power - 2) * (power - 1) * power * Math.Pow(x, power - 3));
            }, start: 1.0, steps: 1000);
        }, start: 1.0, end: 4, steps: 100);
    }

    // For fun, I am also testing the Hermite polynomial equations.
    // I am not going to refactor those using duals because they are used in hot paths (and our dual implementation is not particularly efficient)

    [Test]
    public void HermiteTest()
    {
        static Dual HermiteQuintic(double p0, double v0, double a0, double a1, double v1, double p1, Dual t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            var t4 = t3 * t;
            var t5 = t4 * t;

            var h0 = 1d - 10d * t3 + 15d * t4 - 6d * t5;
            var h1 = t - 6d * t3 + 8d * t4 - 3d * t5;
            var h2 = 1d / 2d * t2 - 3d / 2d * t3 + 3d / 2d * t4 - 1d / 2d * t5;
            var h3 = 1d / 2d * t3 - t4 + 1d / 2d * t5;
            var h4 = -4d * t3 + 7d * t4 - 3d * t5;
            var h5 = 10d * t3 - 15d * t4 + 6d * t5;

            return h0 * p0 + h1 * v0 + h2 * a0 +
                   h3 * a1 + h4 * v1 + h5 * p1;
        }

        RangeScanRec(vector =>
        {
           RangeScan((t, tDual) =>
           {
               var p0 = vector[0];
               var v0 = vector[1];
               var a0 = vector[2];
               var a1 = vector[3];
               var v1 = vector[4];
               var p1 = vector[5];

               var hermite = HermiteQuintic(p0, v0, a0, a1, v1, p1, tDual);
              
               var y = Splines.HermiteQuintic(p0, v0, a0, a1, v1, p1, t);
               var dy = Splines.HermiteQuinticDerivative1(p0, v0, a0, a1, v1, p1, t);
               var ddy = Splines.HermiteQuinticDerivative2(p0, v0, a0, a1, v1, p1, t);

               AreEqual(hermite.Value, y);
               AreEqual(hermite[1], dy);
               AreEqual(hermite[2], ddy);
           }, derivatives: 2, start: 0, end: 1, steps: 10);
        }, 6, -100, 100, 5);
    }
}