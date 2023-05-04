using Coyote.Mathematics;

namespace Coyote.Tests;

public class GeometryTests
{
    /**
     *  TODO what would be the best test for the dual variants? Even though they are copy-pasted from the non-dual implementations tested below, and just had their Double replaced with Duals (which have unit tests),
     *  I would still like some tests for them.
     */
    private const double Eps = 10e-12;

    [Test]
    public void TestVector2d()
    {
        Assert.That(Vector2d.Zero, Is.EqualTo(new Vector2d(0, 0)));
        Assert.That(Vector2d.One, Is.EqualTo(new Vector2d(1, 1)));
        Assert.That(Vector2d.UnitX, Is.EqualTo(new Vector2d(1, 0)));
        Assert.That(Vector2d.UnitY, Is.EqualTo(new Vector2d(0, 1)));
        Assert.That(Vector2d.Zero.Length, Is.EqualTo(0));
        Assert.That(Vector2d.One.Length, Is.EqualTo(Math.Sqrt(2)));
        Assert.That(Vector2d.UnitX.Length, Is.EqualTo(1));
        Assert.That(Vector2d.UnitY.Length, Is.EqualTo(1));

        Assert.That(Vector2d.One * 0.5 + Vector2d.One * 0.5, Is.EqualTo(Vector2d.One).And.EqualTo(new Vector2d(1, 1)).And.EqualTo(Vector2d.One * 2 - Vector2d.One));
        Assert.That(Vector2d.One * 0.5, Is.EqualTo(Vector2d.One / 2).And.EqualTo(new Vector2d(0.5, 0.5)));
        Assert.That(Vector2d.One * Vector2d.One, Is.EqualTo(Vector2d.One));
        Assert.That(Vector2d.One * Vector2d.Zero, Is.EqualTo(Vector2d.Zero));
        
        Assert.That(Vector2d.One, Is.Not.EqualTo(Vector2d.Zero));
        
        Assert.That(Vector2d.UnitX + Vector2d.UnitY, Is.EqualTo(Vector2d.One));
        Assert.That(new Vector2d(5, 6).LengthSqr, Is.EqualTo(5 * 5 + 6 * 6));
        Assert.That(new Vector2d(50, 20).Normalized().Length, Is.EqualTo(1));
        Assert.That(new Vector2d(50, 20).Normalized() * new Vector2d(50, 20).Length, Is.EqualTo(new Vector2d(50, 20)));
    }

    [Test]
    public void TestRotation2d()
    {
        void AreEqual(params Rotation2d[] values)
        {
            if (values.Length <= 1)
            {
                return;
            }

            for (var i = 1; i < values.Length; i++)
            {
                Assert.IsTrue(values[i - 1].ApproxEqs(values[i]));
            }
        }

        var rpi = Rotation2d.Exp(Math.PI);

        AreEqual(rpi, rpi);

        Assert.That(rpi.Scaled(1.0), Is.EqualTo(rpi));
        Assert.That(rpi.Scaled(-1), Is.EqualTo(rpi.Inverse));
        Assert.That(rpi.Scaled(0.5), Is.EqualTo(Rotation2d.Exp(Math.PI / 2)));

        AreEqual(rpi * rpi, Rotation2d.Exp(Math.PI * 2), Rotation2d.Zero);
        AreEqual(rpi * Rotation2d.Exp(-Math.PI), Rotation2d.Zero);
        AreEqual(rpi * rpi.Inverse, Rotation2d.Zero);
        Assert.IsTrue((rpi * Vector2d.UnitX).ApproxEqs(-Vector2d.UnitX, Eps));
        Assert.IsTrue((Rotation2d.Exp(Math.PI * 2) * Vector2d.UnitX).ApproxEqs(Vector2d.UnitX, Eps));
        Assert.IsTrue((Rotation2d.Exp(Math.PI * 8) * Vector2d.UnitX).ApproxEqs(Vector2d.UnitX, Eps));

        Assert.That(Rotation2d.Interpolate(Rotation2d.Zero, rpi, 0.0), Is.EqualTo(Rotation2d.Zero));
        Assert.That(Rotation2d.Interpolate(Rotation2d.Zero, rpi, 1.0), Is.EqualTo(rpi));
        Assert.That(Rotation2d.Interpolate(Rotation2d.Zero, rpi, 0.5), Is.EqualTo(Rotation2d.Exp(Math.PI / 2)));
        Assert.That(Rotation2d.Interpolate(Rotation2d.Zero, rpi, 0.25), Is.EqualTo(Rotation2d.Exp(Math.PI / 4)));

        Assert.That(Rotation2d.Interpolate(Rotation2d.Zero, rpi, 0.25), Is.EqualTo(Rotation2d.Exp(Math.PI / 4)));

        Utilities.RangeScan(t =>
        {
            AreEqual(Rotation2d.Interpolate(rpi, rpi, t), rpi);
        }, start: 0, end: 1);

        Utilities.RangeScanRec(vec =>
        {
            var a = Rotation2d.Exp(vec[0]);
            var b = Rotation2d.Exp(vec[1]);

            AreEqual(a * (b / a), b);
        }, 2, start: -100, end: 100, steps: 1000);
    }

    [Test]
    public void TestPose()
    {
        void AreEqual(params Pose2d[] values)
        {
            if (values.Length <= 1)
            {
                return;
            }

            for (var i = 1; i < values.Length; i++)
            {
                Assert.IsTrue(values[i - 1].ApproxEqs(values[i]));
            }
        }

        Utilities.RangeScanRec(vec =>
        {
            var a = new Pose2d(vec[0], vec[1], vec[2]);
            var b = new Pose2d(vec[3], vec[4], vec[5]);

            AreEqual(a + (b - a), b);
            AreEqual(a * (b / a), b);
        }, start: -100, end: 100, steps: 10, layers: 6);
    }
}