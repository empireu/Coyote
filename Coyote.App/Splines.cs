using System;
using System.Numerics;
using GameFramework.Extensions;
using GameFramework.Renderer.Batch;
using Veldrid;

namespace Coyote.App;

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

        dx = Splines.HermiteQuintic(segment.P0.X, segment.V0.X, segment.A0.X, segment.A1.X, segment.V1.X, segment.P1.X, t);
        dy = Splines.HermiteQuintic(segment.P0.Y, segment.V0.Y, segment.A0.Y, segment.A1.Y, segment.V1.Y, segment.P1.Y, t);
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

            result += (double)(Vector2.Distance(Evaluate((float)t0), Evaluate((float)t1)));
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

        return closest;
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

    public static Vector2 HermiteQuintic2(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1, double t)
    {
        return new Vector2(
            (float)HermiteQuintic(p0.X, v0.X, a0.X, a1.X, v1.X, p1.X, t),
            (float)HermiteQuintic(p0.Y, v0.Y, a0.Y, a1.Y, v1.Y, p1.Y, t));
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
}