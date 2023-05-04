using System;
using System.Diagnostics;
using Coyote.Mathematics;

namespace Coyote.Tests;

public class GeometryTests
{
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
        
        Utilities.RangeScan(t =>
        {
            AreEqual(Rotation2d.Interpolate(rpi, rpi, t), rpi);
        }, start: 0, end: 1);
    }
}