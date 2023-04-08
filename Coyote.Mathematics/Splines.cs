using System.Numerics;
using Coyote.Data;
using GameFramework.Utilities;

namespace Coyote.Mathematics;

#region Interfaces

public interface IPositionSpline
{
    Translation EvaluateTranslation(Real<Percentage> progress);
}

public interface IVelocitySpline
{
    Real2<Velocity> EvaluateVelocity(Real<Percentage> progress);
}

public interface IAccelerationSpline
{
    Real2<Acceleration> EvaluateAcceleration(Real<Percentage> progress);
}

public interface ICurvatureSpline
{
    Real<Curvature> EvaluateCurvature(Real<Percentage> progress);
}

public interface ICurvePoseSpline
{
    CurvePose EvaluateCurvePose(Real<Percentage> progress);
}

#endregion

public sealed class ArcParameterizedQuinticSpline
{
    private const int Segments = 8192;

    private readonly struct ArcSegment
    {
        public ArcSegment(Real<Displacement> arc0, Real<Displacement> arc1, Real<Percentage> t0, Real<Percentage> t1)
        {
            Arc0 = arc0;
            Arc1 = arc1;
            T0 = t0;
            T1 = t1;
        }

        public Real<Displacement> Arc0 { get; }
        public Real<Displacement> Arc1 { get; }
        public Real<Percentage> T0 { get; }
        public Real<Percentage> T1 { get; }

        public Real<Percentage> EvaluateParameter(Real<Displacement> arc)
        {
            return arc.Clamped(Arc0, Arc1).MappedTo(Arc0, Arc1, T0, T1);
        }
    }

    private readonly QuinticSpline _spline = new();

    public Real2<Displacement> EvaluateUnderlying(Real<Percentage> t)
    {
        return _spline.Evaluate(t);
    }

    public Real2<Velocity> EvaluateUnderlyingVelocity(Real<Percentage> t)
    {
        return _spline.EvaluateVelocity(t);
    }

    private readonly SegmentTree<ArcSegment> _segmentTree;

    public ArcParameterizedQuinticSpline(IEnumerable<QuinticSplineSegment> segments)
    {
        foreach (var quinticSplineSegment in segments)
        {
            _spline.Add(quinticSplineSegment);
        }

        var builder = new SegmentTreeBuilder<ArcSegment>();

        var currentArc = Real<Displacement>.Zero;

        for (var segmentIndex = 1; segmentIndex < Segments; segmentIndex++)
        {
            var t0 = ((segmentIndex - 1) / (Segments - 1f)).ToReal<Percentage>();
            var t1 = (segmentIndex / (Segments - 1f)).ToReal<Percentage>();

            var segmentLength = Real2<Displacement>.Distance(_spline.Evaluate(t0), _spline.Evaluate(t1));

            var arcEnd = currentArc + segmentLength;

            builder.Insert(new ArcSegment(currentArc, arcEnd, t0, t1), new SegmentRange(currentArc, arcEnd));

            currentArc = arcEnd;
        }

        ArcLength = currentArc;

        _segmentTree = builder.Build();
    }

    public double ArcLength { get; }

    public Real<Percentage> EvaluateParameter(Real<Displacement> arcLength)
    {
        arcLength = Real<Displacement>.Clamp(arcLength, _segmentTree.Range.Start, _segmentTree.Range.End);

        return _segmentTree.Query(arcLength.Value).EvaluateParameter(arcLength);
    }

    public Real2<Displacement> Evaluate(Real<Displacement> arcLength)
    {
        return _spline.Evaluate(EvaluateParameter(arcLength));
    }

    public Real2<Velocity> EvaluateVelocity(Real<Displacement> arcLength)
    {
        return _spline.EvaluateVelocity(EvaluateParameter(arcLength));
    }
}

public readonly struct QuinticSplineSegment
{
    public Real2<Displacement> P0 { get; }
    public Real2<Velocity> V0 { get; }
    public Real2<Acceleration> A0 { get; }
    public Real2<Acceleration> A1 { get; }
    public Real2<Velocity> V1 { get; }
    public Real2<Displacement> P1 { get; }

    public QuinticSplineSegment(Real2<Displacement> p0, Real2<Velocity> v0, Real2<Acceleration> a0, Real2<Acceleration> a1, Real2<Velocity> v1, Real2<Displacement> p1)
    {
        P0 = p0;
        V0 = v0;
        A0 = a0;
        A1 = a1;
        V1 = v1;
        P1 = p1;
    }

    public Real2<Displacement> Evaluate(Real<Percentage> t)
    {
        return Splines.HermiteQuintic2(P0, V0, A0, A1, V1, P1, t);
    }

    public Real2<Velocity> EvaluateVelocity(Real<Percentage> t)
    {
        return Splines.HermiteQuintic2Derivative1(P0, V0, A0, A1, V1, P1, t);
    }

    public Real2<Acceleration> EvaluateAcceleration(Real<Percentage> t)
    {
        return Splines.HermiteQuintic2Derivative2(P0, V0, A0, A1, V1, P1, t);
    }

    public Real<Curvature> EvaluateCurvature(Real<Percentage> t)
    {
        return Splines.HermiteQuintic2Curvature(P0, V0, A0, A1, V1, P1, t);
    }
}

/// <summary>
///     Represents a piecewise quintic polynomial with a 0-1 parameter range.
/// </summary>
public sealed class QuinticSpline : IPositionSpline, IVelocitySpline, IAccelerationSpline, ICurvatureSpline, ICurvePoseSpline
{
    /// <summary>
    ///     The number of samples to use for the rough projection estimate.
    /// </summary>
    private const int ProjectionSamples = 128;

    /// <summary>
    ///     The number of descent steps to use for optimizing the rough projection estimate.
    /// </summary>
    private const int DescentSteps = 32;

    /// <summary>
    ///     The overshoot descent rate falloff to use.
    /// </summary>
    private const double DescentFalloff = 1.25;

    public List<QuinticSplineSegment> Segments { get; } = new();

    public void Add(QuinticSplineSegment segment)
    {
        Segments.Add(segment);
    }

    public void Clear()
    {
        Segments.Clear();
    }

    public void Evaluate(double progress, out double dx, out double dy)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        var segment = Segments[index];

        dx = Splines.HermiteQuintic(segment.P0.X, segment.V0.X, segment.A0.X, segment.A1.X, segment.V1.X, segment.P1.X, t);
        dy = Splines.HermiteQuintic(segment.P0.Y, segment.V0.Y, segment.A0.Y, segment.A1.Y, segment.V1.Y, segment.P1.Y, t);
    }

    public Real2<Displacement> Evaluate(Real<Percentage> progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        var position = Segments[index].Evaluate(t);

        return new Real2<Displacement>(new Real<Displacement>(position.X), new Real<Displacement>(position.Y));
    }

    public Real<Displacement> ComputeArcLength(int points = 1024)
    {
        double result = 0;

        var sampleSize = 1d / points;

        for (var i = 1; i < points; i++)
        {
            var t0 = (i - 1) * sampleSize;
            var t1 = t0 + sampleSize;

            result += Real2<Displacement>.Distance(
                Evaluate(t0.ToReal<Percentage>()), 
                Evaluate(t1.ToReal<Percentage>()));
        }

        return result.ToReal<Displacement>();
    }

    public Real<Percentage> Project(Real2<Displacement> position)
    {
        if (Segments.Count == 0)
        {
            return Real<Percentage>.Zero;
        }

        var closest = Real<Percentage>.Zero;
        var closestDistance = Real<Displacement>.MaxValue;

        for (var sample = 0; sample < ProjectionSamples; sample++)
        {
            var t = (sample / (ProjectionSamples - 1d)).ToReal<Percentage>();

            var splinePoint = Evaluate(t);

            var distance = Real2<Displacement>.DistanceSquared(position, splinePoint);

            if (distance < closestDistance)
            {
                closest = t;
                closestDistance = distance;
            }
        }

        Real<Displacement> ProjectError(Real<Percentage> t)
        {
            var p = Evaluate(t.Clamped(0, 1));

            return Real2<Displacement>.DistanceSquared(p, position);
        }

        var optimizedClosest = closest;
        var descentRate = (1d / ProjectionSamples).ToReal<Percentage>();

        for (var i = 0; i < DescentSteps; i++)
        {
            var errorLeft = ProjectError(optimizedClosest - descentRate);
            var errorRight = ProjectError(optimizedClosest + descentRate);

            var step = -descentRate;
            var adjustedError = errorLeft;

            if (errorRight < errorLeft)
            {
                step = descentRate;
                adjustedError = errorRight;
            }

            var currentError = ProjectError(optimizedClosest);

            if (adjustedError > currentError)
            {
                descentRate = descentRate.Pow(DescentFalloff);

                continue;
            }

            optimizedClosest += step;
        }

        return optimizedClosest;
    }

    public Pose EvaluatePose(Real<Percentage> progress)
    {
        return new Pose(EvaluateTranslation(progress), EvaluateVelocity(progress).ToRotation());
    }

    #region Interface

    public Translation EvaluateTranslation(Real<Percentage> progress)
    {
        return Evaluate(progress);
    }

    public Real2<Velocity> EvaluateVelocity(Real<Percentage> progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateVelocity(t);
    }

    public Real2<Acceleration> EvaluateAcceleration(Real<Percentage> progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateAcceleration(t);
    }

    public Real<Curvature> EvaluateCurvature(Real<Percentage> progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateCurvature(t);
    }

    public CurvePose EvaluateCurvePose(Real<Percentage> progress)
    {
        return new CurvePose(EvaluatePose(progress), EvaluateCurvature(progress));
    }

    #endregion
}

public readonly struct CubicSplineSegment
{
    public CubicSplineSegment(double keyStart, double keyEnd, Real<Displacement> p0, Real<Velocity> v0, Real<Velocity> v1, Real<Displacement> p1)
    {
        KeyStart = keyStart;
        KeyEnd = keyEnd;
        P0 = p0;
        V0 = v0;
        V1 = v1;
        P1 = p1;
    }

    public double KeyStart { get; }
    public double KeyEnd { get; }

    public Real<Displacement> P0 { get; }
    public Real<Velocity> V0 { get; }
    public Real<Velocity> V1 { get; }
    public Real<Displacement> P1 { get; }

    public double Evaluate(double t)
    {
        return Splines.HermiteCubic(P0, V0, V1, P1, t);
    }
}

/// <summary>
///     Represents a piecewise cubic polynomial with an arbitrary parameter range.
/// </summary>
public sealed class MappedCubicSpline
{
    private readonly List<CubicSplineSegment> _segments = new();

    public IReadOnlyList<CubicSplineSegment> Segments => _segments;

    public bool IsEmpty => Segments.Count == 0;

    public void Insert(CubicSplineSegment segment)
    {
        if (segment.KeyEnd <= segment.KeyStart)
        {
            throw new Exception("Invalid segment direction");
        }

        if (_segments.Count > 0)
        {
            var last = _segments.Last();

            if (!last.KeyEnd.Equals(segment.KeyStart))
            {
                throw new Exception("Spline continuity broken");
            }
        }

        _segments.Add(segment);
    }

    public double StartKey => _segments.First().KeyStart;

    public double EndKey => _segments.Last().KeyEnd;

    public double Evaluate(double key)
    {
        if (_segments.Count == 0)
        {
            throw new InvalidOperationException("Cannot evaluate spline with 0 segments");
        }

        if (key <= StartKey)
        {
            return _segments.First().Evaluate(Real<Percentage>.Zero);
        }

        if (key >= EndKey)
        {
            return _segments.Last().Evaluate(Real<Percentage>.One);
        }

        CubicSplineSegment segment = default;

        var found = false;

        for (var index = 0; index < _segments.Count; index++)
        {
            segment = _segments[index];

            if (segment.KeyStart <= key && segment.KeyEnd >= key)
            {
                found = true;

                break;
            }
        }

        if (!found)
        {
            // Shouldn't happen since we checked continuities on adding and we have those key conditions at the start of the method.

            throw new Exception("Did not find segment");
        }

        var progress = MathUtilities.MapRange(
            key,
            segment.KeyStart,
            segment.KeyEnd,
            0,
            1);

        return segment.Evaluate(progress);
    }

    public void Clear()
    {
        _segments.Clear();
    }
}

public sealed class MappedCubicSplineBuilder
{
    private struct SplinePoint
    {
        public double Key;
        public double Value;
        public double Tension;
    }

    private readonly List<SplinePoint> _points = new();

    public void Add(double key, double value, double tension = 1)
    {
        if (_points.Count > 0)
        {
            if (key <= _points.Last().Key)
            {
                throw new ArgumentException("Key precedes the key of the previous point.");
            }
        }

        _points.Add(new SplinePoint
        {
            Key = key,
            Value = value,
            Tension = tension
        });
    }

    public bool IsValid()
    {
        return _points.Count > 1;
    }

    public void Build(MappedCubicSpline spline)
    {
        if (!IsValid())
        {
            throw new InvalidOperationException("Cannot build spline with specified point set");
        }

        SplinePoint Get(int index)
        {
            if (index < 0)
            {
                return _points[0];
            }

            if (index >= _points.Count)
            {
                return _points.Last();
            }

            return _points[index];
        }

        for (var i = 1; i < _points.Count; i++)
        {
            var previous = Get(i - 2);
            var left = Get(i - 1);
            var right = Get(i);
            var next = Get(i + 1);

            spline.Insert(new CubicSplineSegment(
                left.Key,
                right.Key,
                left.Value.ToReal<Displacement>(),
                (previous.Value * left.Tension).ToReal<Velocity>(),
                (next.Value * right.Tension).ToReal<Velocity>(),
                right.Value.ToReal<Displacement>()));
        }
    }

    public void Clear()
    {
        _points.Clear();
    }
}

public static class Splines
{
    public static void GetUniformIndices(int segments, double progress, out int index, out double t)
    {
        progress = Math.Clamp(progress, 0, 1);
        progress *= segments;
        index = Math.Clamp((int)progress, 0, segments - 1);
        t = progress - index;
    }

    public static void GetUniformIndices(int segments, Real<Percentage> progress, out int index, out Real<Percentage> t)
    {
        GetUniformIndices(segments, progress.Value, out index, out var t2);
        t = ((float)t2).ToReal<Percentage>();
    }

    public static double HermiteCubic(double p0, double v0, double v1, double p1, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        var h1 = -v0 / 2d + 3d * p0 / 2d - 3d * p1 / 2d + v1 / 2d;
        var h2 = v0 - 5d * p0 / 2d + 2d * p1 - v1 / 2d;
        var h3 = -v0 / 2d + p1 / 2d;

        return h1 * t3 + h2 * t2 + h3 * t + p0;
    }

    public static Vector2 HermiteCubic2(Vector2 p0, Vector2 v0, Vector2 v1, Vector2 p1, float t)
    {
        return new Vector2(
            (float)HermiteCubic(p0.X, v0.X, v1.X, p1.X, t),
            (float)HermiteCubic(p0.Y, v0.Y, v1.Y, p1.Y, t));
    }

    public static double HermiteQuintic(double p0, double v0, double a0, double a1, double v1, double p1, double t)
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

    public static Vector2 HermiteQuintic2(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1,
        double t)
    {
        return new Vector2(
            (float)HermiteQuintic(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            (float)HermiteQuintic(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }

    public static Real2<Displacement> HermiteQuintic2(
        Real2<Displacement> p0, 
        Real2<Velocity> v0, 
        Real2<Acceleration> a0, 
        Real2<Acceleration> a1, 
        Real2<Velocity> v1, 
        Real2<Displacement> p1,
        Real<Percentage> t)
    {
        return new Real2<Displacement>(
            HermiteQuintic(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            HermiteQuintic(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }

    public static double HermiteQuinticDerivative1(double p0, double v0, double a0, double a1, double v1, double p1, double t)
    {
        var t2 = t * t;

        var h0 = -30d * ((t - 1d) * (t - 1d)) * t2;
        var h1 = -((t - 1d) * (t - 1d)) * (15d * t2 - 2d * t - 1d);
        var h2 = -1d / 2d * ((t - 1d) * (t - 1d)) * t * (5d * t - 2d);
        var h3 = 1d / 2d * t2 * (5d * t2 - 8d * t + 3d);
        var h4 = t2 * (-15d * t2 + 28d * t - 12d);
        var h5 = 30d * ((t - 1d) * (t - 1d)) * t2;

        return h0 * p0 + h1 * v0 + h2 * a0 +
               h3 * a1 + h4 * v1 + h5 * p1;
    }

    public static Vector2 HermiteQuintic2Derivative1(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1, double t)
    {
        return new Vector2(
            (float)HermiteQuinticDerivative1(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            (float)HermiteQuinticDerivative1(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }

    public static Real2<Velocity> HermiteQuintic2Derivative1(
        Real2<Displacement> p0,
        Real2<Velocity> v0,
        Real2<Acceleration> a0,
        Real2<Acceleration> a1,
        Real2<Velocity> v1,
        Real2<Displacement> p1,
        Real<Percentage> t)
    {
        return new Real2<Velocity>(
            HermiteQuinticDerivative1(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            HermiteQuinticDerivative1(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }

    public static double HermiteQuinticDerivative2(double p0, double v0, double a0, double a1, double v1, double p1, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        var h0 = -60 * t * (2 * t2 - 3 * t + 1);
        var h1 = -12 * t * (5 * t2 - 8 * t + 3);
        var h2 = -10 * t3 + 18 * t2 - 9 * t + 1;
        var h3 = t * (10 * t2 - 12 * t + 3);
        var h4 = -12 * t * (5 * t2 - 7 * t + 2);
        var h5 = 60 * t * (2 * t2 - 3 * t + 1);

        return h0 * p0 + h1 * v0 + h2 * a0 +
               h3 * a1 + h4 * v1 + h5 * p1;
    }

    public static Vector2 HermiteQuintic2Derivative2(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1, double t)
    {
        return new Vector2(
            (float)HermiteQuinticDerivative2(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            (float)HermiteQuinticDerivative2(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }

    public static Real2<Acceleration> HermiteQuintic2Derivative2(
        Real2<Displacement> p0,
        Real2<Velocity> v0,
        Real2<Acceleration> a0,
        Real2<Acceleration> a1,
        Real2<Velocity> v1,
        Real2<Displacement> p1,
        Real<Percentage> t)
    {
        return new Real2<Acceleration>(
            HermiteQuinticDerivative2(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            HermiteQuinticDerivative2(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }

    public static Real<Curvature> HermiteQuintic2Curvature(
        Real2<Displacement> p0,
        Real2<Velocity> v0,
        Real2<Acceleration> a0,
        Real2<Acceleration> a1,
        Real2<Velocity> v1,
        Real2<Displacement> p1,
        Real<Percentage> t)
    {
        var dx = HermiteQuinticDerivative1(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t);
        var dy = HermiteQuinticDerivative1(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t);
        var ddx = HermiteQuinticDerivative2(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t);
        var ddy = HermiteQuinticDerivative2(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t);

        return new Real<Curvature>((dx * ddy - ddx * dy) / ((dx * dx + dy * dy) * Math.Sqrt(dx * dx + dy * dy)));
    }
}