using System.Numerics;
using GameFramework.Extensions;
using GameFramework.Renderer.Batch;
using GameFramework.Utilities;
using Veldrid;

namespace Coyote.App.Movement;

class ArcParameterizedQuinticSpline
{
    private const int Segments = 8192;

    private readonly struct ArcSegment
    {
        public ArcSegment(float arc0, float arc1, float t0, float t1)
        {
            Arc0 = arc0;
            Arc1 = arc1;
            T0 = t0;
            T1 = t1;
        }

        public float Arc0 { get; }
        public float Arc1 { get; }
        public float T0 { get; }
        public float T1 { get; }

        public float EvaluateParameter(float arc)
        {
            return MathUtilities.MapRange(Math.Clamp(arc, Arc0, Arc1), Arc0, Arc1, T0, T1);
        }
    }

    private readonly UniformQuinticSpline _spline = new();

    public Vector2 EvaluateUnderlying(float t)
    {
        return _spline.Evaluate(t);
    }

    public Vector2 EvaluateUnderlyingDerivative1(float t)
    {
        return _spline.EvaluateDerivative1(t);
    }

    private readonly SegmentTree<ArcSegment> _segmentTree;
    
    public ArcParameterizedQuinticSpline(IEnumerable<QuinticSplineSegment> segments)
    {
        foreach (var quinticSplineSegment in segments)
        {
            _spline.Add(quinticSplineSegment);
        }

        var builder = new SegmentTreeBuilder<ArcSegment>();

        var currentArc = 0f;

        for (var segmentIndex = 1; segmentIndex < Segments; segmentIndex++)
        {
            var t0 = (segmentIndex - 1) / (Segments - 1f);
            var t1 = segmentIndex / (Segments - 1f);

            var segmentLength = Vector2.Distance(_spline.Evaluate(t0), _spline.Evaluate(t1));

            var arcEnd = currentArc + segmentLength;

            builder.Insert(new ArcSegment(currentArc, arcEnd, t0, t1), new SegmentRange(currentArc, arcEnd));

            currentArc = arcEnd;
        }

        ArcLength = currentArc;

        _segmentTree = builder.Build();
    }

    public float ArcLength { get; }

    public float EvaluateParameter(float arcLength)
    {
        arcLength = Math.Clamp(arcLength, _segmentTree.Range.Start, _segmentTree.Range.End);

        return _segmentTree.Query(arcLength).EvaluateParameter(arcLength);
    }

    public Vector2 Evaluate(float arcLength)
    {
        return _spline.Evaluate(EvaluateParameter(arcLength));
    }

    public Vector2 EvaluateDerivative1(float arcLength)
    {
        return _spline.EvaluateDerivative1(EvaluateParameter(arcLength));
    }
}

internal readonly struct QuinticSplineSegment
{
    public Vector2 P0 { get; }
    public Vector2 V0 { get; }
    public Vector2 A0 { get; }
    public Vector2 A1 { get; }
    public Vector2 V1 { get; }
    public Vector2 P1 { get; }

    public QuinticSplineSegment(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1)
    {
        P0 = p0;
        V0 = v0;
        A0 = a0;
        A1 = a1;
        V1 = v1;
        P1 = p1;
    }

    public Vector2 Evaluate(float t)
    {
        return Splines.HermiteQuintic2(P0, V0, A0, A1, V1, P1, t);
    }

    public Vector2 EvaluateDerivative1(float t)
    {
        return Splines.HermiteQuintic2Derivative1(P0, V0, A0, A1, V1, P1, t);
    }
}

internal class UniformQuinticSpline
{
    private const int ProjectionSamples = 128;
    private const int DescentSteps = 32;
    private const double DescentFalloff = 1.25;

    private const int SamplesPerSegment = 64;
    private static readonly RgbaFloat LineColor = new(0.9f, 1f, 1f, 0.9f);
    private const float LineThickness = 0.015f;

    public List<QuinticSplineSegment> Segments { get; } = new();

    private Vector2[] _renderPoints = Array.Empty<Vector2>();

    public void Clear()
    {
        Segments.Clear();
        ClearRenderPoints();
    }

    public void Add(QuinticSplineSegment segment)
    {
        Segments.Add(segment);
    }

    public void ClearRenderPoints()
    {
        _renderPoints = Array.Empty<Vector2>();
    }

    public void UpdateRenderPoints()
    {
        if (Segments.Count == 0)
        {
            return;
        }

        var points = new List<Vector2>();
        var samples = Segments.Count * SamplesPerSegment;
        for (var subSampleIndex = 0; subSampleIndex < samples; subSampleIndex++)
        {
            var t0 = subSampleIndex / (samples - 1f);

            points.Add(Evaluate(t0));
        }

        _renderPoints = points.ToArray();
    }

    public void Render(QuadBatch batch, Func<Vector2, Vector2>? mapping = null)
    {
        if (_renderPoints.Length < 2)
        {
            return;
        }

        for (var i = 1; i < _renderPoints.Length; i++)
        {
            var start = _renderPoints[i - 1];
            var end = _renderPoints[i];

            if (mapping != null)
            {
                start = mapping(start);
                end = mapping(end);
            }

            batch.Line(start, end, LineColor, LineThickness);
        }
    }

    public Vector2 Evaluate(float progress)
    {
        Splines.GetIndicesUniform(Segments.Count, progress, out var index, out var t);
        return Segments[index].Evaluate((float)t);
    }

    public void Evaluate(double progress, out double dx, out double dy)
    {
        Splines.GetIndicesUniform(Segments.Count, progress, out var index, out var t);
        var segment = Segments[index];

        dx = Splines.HermiteQuintic(segment.P0.X, segment.V0.X, segment.A0.X, segment.A1.X, segment.V1.X, segment.P1.X,
            t);
        dy = Splines.HermiteQuintic(segment.P0.Y, segment.V0.Y, segment.A0.Y, segment.A1.Y, segment.V1.Y, segment.P1.Y,
            t);
    }

    public Vector2 EvaluateDerivative1(float progress)
    {
        Splines.GetIndicesUniform(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateDerivative1((float)t);
    }

    public float ComputeArcLength(int points = 1024)
    {
        double result = 0;

        var sampleSize = 1d / points;

        for (var i = 1; i < points; i++)
        {
            var t0 = (i - 1) * sampleSize;
            var t1 = t0 + sampleSize;

            result += (double)Vector2.Distance(Evaluate((float)t0), Evaluate((float)t1));
        }

        return (float)result;
    }

    public float Project(Vector2 position)
    {
        if (Segments.Count == 0)
        {
            return 0;
        }

        var closest = 0f;
        var closestDistance = float.MaxValue;

        for (var sample = 0; sample < ProjectionSamples; sample++)
        {
            var t = sample / (ProjectionSamples - 1f);

            var splinePoint = Evaluate(t);

            var distance = Vector2.DistanceSquared(position, splinePoint);

            if (distance < closestDistance)
            {
                closest = t;
                closestDistance = distance;
            }
        }

        static double CentralFiniteDifference(Func<double, double> function, double x, double epsilon)
        {
            return (function(x + epsilon) - function(x - epsilon)) / (2d * epsilon);
        }

        double ProjectError(double t)
        {
            Evaluate(Math.Clamp(t, 0, 1), out var x, out var y);

            var dx = x - position.X;
            var dy = y - position.Y;

            return dx * dx + dy * dy;
        }

        var optimizedClosest = (double)closest;
        var descentRate = 1d / ProjectionSamples;

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
                descentRate = Math.Pow(descentRate, DescentFalloff);

                continue;
            }

            optimizedClosest += step;
        }

        return (float)optimizedClosest;
    }
}

internal readonly struct CubicSplineSegment
{
    public CubicSplineSegment(double keyStart, double keyEnd, double p0, double v0, double v1, double p1)
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

    public double P0 { get; }
    public double V0 { get; }
    public double V1 { get; }
    public double P1 { get; }

    public double Evaluate(double t)
    {
        return Splines.HermiteCubic(P0, V0, V1, P1, t);
    }
}

internal class MappedCubicSpline
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
            return _segments.First().Evaluate(0);
        }

        if (key >= EndKey)
        {
            return _segments.Last().Evaluate(1);
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

internal class MappedCubicSplineBuilder
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
                left.Value,
                previous.Value * left.Tension,
                next.Value * right.Tension,
                right.Value));
        }
    }

    public void Clear()
    {
        _points.Clear();
    }
}

internal static class Splines
{
    public static void GetIndicesUniform(int segments, double progress, out int index, out double t)
    {
        progress = Math.Clamp(progress, 0, 1);
        progress *= segments;
        index = Math.Clamp((int)progress, 0, segments - 1);
        t = progress - index;
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

    public static double HermiteQuinticDerivative1(double p0, double v0, double a0, double a1, double v1, double p1,
        double t)
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

    public static Vector2 HermiteQuintic2Derivative1(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1,
        Vector2 p1, double t)
    {
        return new Vector2(
            (float)HermiteQuinticDerivative1(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            (float)HermiteQuinticDerivative1(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
    }
}