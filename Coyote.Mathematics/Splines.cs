using GameFramework.Utilities;

namespace Coyote.Mathematics;

#region Interfaces

public interface IPositionSpline
{
    Vector EvaluateTranslation(double progress);
}

public interface IVelocitySpline
{
    Vector EvaluateVelocity(double progress);
}

public interface IAccelerationSpline
{
    Vector EvaluateAcceleration(double progress);
}

public interface ICurvatureSpline
{
    double EvaluateCurvature(double progress);
}

public interface ICurvePoseSpline
{
    CurvePose EvaluateCurvePose(double progress);
}

#endregion

public readonly struct QuinticSplineSegment
{
    public Vector P0 { get; }
    public Vector V0 { get; }
    public Vector A0 { get; }
    public Vector A1 { get; }
    public Vector V1 { get; }
    public Vector P1 { get; }

    public int Size => P0.Size;

    public QuinticSplineSegment(
        Vector p0, 
        Vector v0, 
        Vector a0,
        Vector a1,
        Vector v1,
        Vector p1)
    {
        Vectors.Validate(p0, v0, a0, a1, v1, p1);

        P0 = p0;
        V0 = v0;
        A0 = a0;
        A1 = a1;
        V1 = v1;
        P1 = p1;
    }

    public Vector Evaluate(double t)
    {
        return Splines.HermiteQuinticN(P0, V0, A0, A1, V1, P1, t);
    }

    public Vector EvaluateVelocity(double t)
    {
        return Splines.HermiteQuinticNDerivative1(P0, V0, A0, A1, V1, P1, t);
    }

    public Vector EvaluateAcceleration(double t)
    {
        return Splines.HermiteQuinticNDerivative2(P0, V0, A0, A1, V1, P1, t);
    }

    public double EvaluateCurvature(double t)
    {
        Vectors.Validate(P0, 2);

        return Splines.HermiteQuintic2Curvature(
            P0.ToVector2d(), 
            V0.ToVector2d(), 
            A0.ToVector2d(), 
            A1.ToVector2d(), 
            V1.ToVector2d(), 
            P1.ToVector2d(), 
            t);
    }
}

/// <summary>
///     Represents a piecewise quintic polynomial with a 0-1 parameter range.
/// </summary>
public sealed class QuinticSpline : IPositionSpline, IVelocitySpline, IAccelerationSpline, ICurvatureSpline, ICurvePoseSpline
{
    public int Dimensions { get; }

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

    public QuinticSpline(int dimensions)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), $"Cannot create {dimensions}D spline");
        }

        Dimensions = dimensions;
    }

    public List<QuinticSplineSegment> Segments { get; } = new();

    public void Add(QuinticSplineSegment segment)
    {
        if (segment.Size != Dimensions)
        {
            throw new ArgumentException($"Cannot add {segment.Size}D segment to a {Dimensions}D spline.");
        }

        Segments.Add(segment);
    }

    public void Clear()
    {
        Segments.Clear();
    }

    public Vector Evaluate(double progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);

        return Segments[index].Evaluate(t);
    }

    public double ComputeArcLength(int points = 1024)
    {
        double result = 0;

        var sampleSize = 1d / points;

        for (var i = 1; i < points; i++)
        {
            var t0 = (i - 1) * sampleSize;
            var t1 = t0 + sampleSize;

            result += Vector.Distance(
                Evaluate(t0), 
                Evaluate(t1));
        }

        return result;
    }

    public double Project(Vector position)
    {
        Vectors.Validate(position, Dimensions);

        if (Segments.Count == 0)
        {
            return 0;
        }

        var closest = 0d;
        var closestDistance = double.MaxValue;

        for (var sample = 0; sample < ProjectionSamples; sample++)
        {
            var t = sample / (ProjectionSamples - 1d);

            var splinePoint = Evaluate(t);

            var distance = Vector.DistanceSquared(position, splinePoint);

            if (distance < closestDistance)
            {
                closest = t;
                closestDistance = distance;
            }
        }

        double ProjectError(double t)
        {
            var p = Evaluate(t.Clamped(0, 1));

            return Vector.DistanceSquared(p, position);
        }

        var optimizedClosest = closest;

        if (closest is > 0 and < 1)
        {
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
                    descentRate = descentRate.Pow(DescentFalloff);

                    continue;
                }

                optimizedClosest += step;
            }
        }

        return optimizedClosest.Clamped(0, 1);
    }

    public Pose2d EvaluatePose(double progress)
    {
        if (Dimensions != 2)
        {
            throw new Exception($"Cannot evaluate pose in {Dimensions}D spline");
        }

        return new Pose2d(EvaluateTranslation(progress).ToVector2d(), Rotation2d.Dir(EvaluateVelocity(progress).ToVector2d()));
    }

    #region Interface

    public Vector EvaluateTranslation(double progress)
    {
        return Evaluate(progress);
    }

    public Vector EvaluateVelocity(double progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateVelocity(t);
    }

    public Vector EvaluateAcceleration(double progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateAcceleration(t);
    }

    public double EvaluateCurvature(double progress)
    {
        Splines.GetUniformIndices(Segments.Count, progress, out var index, out var t);
        return Segments[index].EvaluateCurvature(t);
    }

    public CurvePose EvaluateCurvePose(double progress)
    {
        return new CurvePose(EvaluatePose(progress), EvaluateCurvature(progress), progress);
    }

    #endregion
}

public readonly struct QuinticSplineSegmentMapped
{
    public double KeyStart { get; }
    public double KeyEnd { get; }
    public QuinticSplineSegment Spline { get; }

    public QuinticSplineSegmentMapped(double keyStart, double keyEnd, QuinticSplineSegment spline)
    {
        KeyStart = keyStart;
        KeyEnd = keyEnd;
        Spline = spline;
    }
}

public sealed class QuinticSplineMapped
{
    private readonly List<QuinticSplineSegmentMapped> _segments = new();

    public QuinticSplineMapped(int dimensions)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), $"Cannot create {dimensions}D spline");
        }

        Dimensions = dimensions;
    }

    public int Dimensions { get; }

    public IReadOnlyList<QuinticSplineSegmentMapped> Segments => _segments;

    public bool IsEmpty => Segments.Count == 0;

    public void Insert(QuinticSplineSegmentMapped segment)
    {
        if (segment.Spline.Size != Dimensions)
        {
            throw new ArgumentException($"Cannot add {segment.Spline.Size}D segment to a {Dimensions}D spline");
        }

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

    public Vector Evaluate(double key)
    {
        if (_segments.Count == 0)
        {
            throw new InvalidOperationException("Cannot evaluate spline with 0 segments");
        }

        if (key <= StartKey)
        {
            return _segments.First().Spline.Evaluate(0);
        }

        if (key >= EndKey)
        {
            return _segments.Last().Spline.Evaluate(1);
        }

        QuinticSplineSegmentMapped segment = default;

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

        return segment.Spline.Evaluate(progress);
    }

    public void Clear()
    {
        _segments.Clear();
    }
}

public sealed class QuinticSplineMappedBuilder
{
    private struct SplinePoint
    {
        public double Key;
        public Vector Displacement;
        public Vector Velocity;
        public Vector Acceleration;
    }

    public int Size { get; }

    private readonly List<SplinePoint> _points = new();

    public QuinticSplineMappedBuilder(int size)
    {
        Size = size;
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

    public void Add(
        double key,
        Vector displacement, 
        Vector velocity, 
        Vector acceleration)
    {
        _points.Add(new SplinePoint
        {
            Key = key,
            Displacement = displacement,
            Velocity = velocity,
            Acceleration = acceleration
        });
    }

    public void Add(double key, Vector displacement)
    {
        _points.Add(new SplinePoint
        {
            Key = key,
            Displacement = displacement,
            Velocity = Vector.Zero(Size),
            Acceleration = Vector.Zero(Size)
        });
    }

    public bool CanBuild()
    {
        return _points.Count > 1;
    }

    public void Build(QuinticSplineMapped spline)
    {
        if (!CanBuild())
        {
            throw new InvalidOperationException("Cannot build spline with specified point set");
        }

        for (var i = 1; i < _points.Count; i++)
        {
            var a = Get(i - 1);
            var b = Get(i);

            spline.Insert(new QuinticSplineSegmentMapped(
                    a.Key,
                    b.Key,
                    new QuinticSplineSegment(
                        a.Displacement,
                        a.Velocity,
                        a.Acceleration,
                        b.Acceleration,
                        b.Velocity,
                        b.Displacement)
                )
            );
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

    public static double HermiteQuintic2Curvature(
        Vector2d p0,
        Vector2d v0,
        Vector2d a0,
        Vector2d a1,
        Vector2d v1,
        Vector2d p1,
        double t)
    {
        var dx = HermiteQuinticDerivative1(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t);
        var dy = HermiteQuinticDerivative1(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t);
        var ddx = HermiteQuinticDerivative2(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t);
        var ddy = HermiteQuinticDerivative2(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t);

        return (dx * ddy - ddx * dy) / ((dx * dx + dy * dy) * Math.Sqrt(dx * dx + dy * dy));
    }

    public static Vector HermiteQuinticN(
        Vector p0,
        Vector v0,
        Vector a0,
        Vector a1,
        Vector v1,
        Vector p1,
        double t)
    {
        Vectors.Validate(p0, v0, a0, a1, v1, p1);

        Span<double> values = stackalloc double[p0.Size];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = HermiteQuintic(p0[i], v0[i], a0[i], a1[i], v1[i], p1[i], t);
        }

        return new Vector(values);
    }

    public static Vector HermiteQuinticNDerivative1(
        Vector p0,
        Vector v0,
        Vector a0,
        Vector a1,
        Vector v1,
        Vector p1,
        double t)
    {
        Vectors.Validate(p0, v0, a0, a1, v1, p1);

        Span<double> values = stackalloc double[p0.Size];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = HermiteQuinticDerivative1(p0[i], v0[i], a0[i], a1[i], v1[i], p1[i], t);
        }

        return new Vector(values);
    }

    public static Vector HermiteQuinticNDerivative2(
        Vector p0,
        Vector v0,
        Vector a0,
        Vector a1,
        Vector v1,
        Vector p1,
        double t)
    {
        Vectors.Validate(p0, v0, a0, a1, v1, p1);

        Span<double> values = stackalloc double[p0.Size];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = HermiteQuinticDerivative2(p0[i], v0[i], a0[i], a1[i], v1[i], p1[i], t);
        }

        return new Vector(values);
    }

    private readonly struct GetPointsFrame
    {
        public double T0 { get; }
        public double T1 { get; }

        public GetPointsFrame(double t0, double t1)
        {
            T0 = t0;
            T1 = t1;
        }
    }
    
    public static void GetPoints<TSpline>(
        IList<CurvePose> results,
        TSpline spline,
        double t0, 
        double t1,
        double tThreshold,
        Twist2dIncr admissible,
        int maxIterations,
        Func<double, double, bool>? splitCondition = null) where TSpline : ICurvePoseSpline
    {
        if (admissible.TrIncr.X <= 0 || admissible.TrIncr.Y <= 0 || admissible.RotIncr <= 0)
        {
            throw new ArgumentException($"The {nameof(admissible)} twist is invalid.");
        }

        results.Add(spline.EvaluateCurvePose(t0));

        var stack = new Stack<GetPointsFrame>();
        stack.Push(new GetPointsFrame(t0, t1));

        var iterations = 0;
        var t = t0;

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            var start = spline.EvaluateCurvePose(current.T0);
            var end = spline.EvaluateCurvePose(current.T1);

            var twist = (start.Pose / end.Pose).Log();

            if (current.T1 - t > tThreshold || 
                (splitCondition != null && splitCondition(current.T0, current.T1)) || 
                Math.Abs(twist.TrIncr.X) > admissible.TrIncr.X || 
                Math.Abs(twist.TrIncr.Y) > admissible.TrIncr.Y || 
                Math.Abs(twist.RotIncr) > admissible.RotIncr)
            {
                stack.Push(new GetPointsFrame((current.T0 + current.T1) / 2, current.T1));
                stack.Push(new GetPointsFrame(current.T0, (current.T0 + current.T1) / 2));
            }
            else
            {
                results.Add(end);
                t = current.T1;
            }

            if (++iterations >= maxIterations)
            {
                throw new Exception("Malformed spline.");
            }
        }
    }
}