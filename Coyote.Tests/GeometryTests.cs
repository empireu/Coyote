using Coyote.Mathematics;

namespace Coyote.Tests;

public class GeometryTests
{
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
}