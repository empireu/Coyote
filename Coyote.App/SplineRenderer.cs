using System.Numerics;
using Coyote.Mathematics;
using GameFramework.Renderer.Batch;
using GameFramework.Extensions;

namespace Coyote.App;

public sealed class SplineRenderer : IDisposable
{
    // Color for least curvature:
    private static readonly Vector4 Color0 = new(0.9f, 1f, 1f, 0.9f);
    
    // Color for most curvature:
    private static readonly Vector4 Color1 = new(1.0f, 0.1f, 0.1f, 1.0f);

    private const float LineThickness = 0.015f;

    private static readonly Twist2dIncr AdmissibleTwist = new(0.1, 0.1, Math.PI / 32);
    private const int MaxIterations = 1024 * 1024;

    private readonly List<CurvePose> _points = new();

    public void Clear()
    {
        _points.Clear();
    }

    public void Update<TSpline>(TSpline spline) where TSpline : IPositionSpline, ICurvePoseSpline
    {
        _points.Clear();

        Splines.GetPoints(
            _points,
            spline,
            0,
            1,
            0.01,
            AdmissibleTwist,
            MaxIterations);
    }

    public void Submit(QuadBatch batch)
    {
        if (_points.Count < 2)
        {
            return;
        }

        var maxCurvature = _points.Max(x => x.Curvature.Abs());

        maxCurvature += MathExt.SnzEps(maxCurvature);

        void Draw(CurvePose start, CurvePose end)
        {
            var k = end.Curvature.Abs();

            batch.Line(
                start.Pose.Translation,
                end.Pose.Translation,
                Vector4.Lerp(
                    Color0,
                    Color1,
                    (float)((k + MathExt.SnzEps(k)) / maxCurvature)), LineThickness);
        }

        for (var i = 1; i < _points.Count; i++)
        {
            var start = _points[i - 1];
            var end = _points[i];

            Draw(start, end);
        }

        if (_points.Count > 2)
        {
            // Special second pass to remove some gaps in the curve:
            for (var i = 1; i < _points.Count - 1; i++)
            {
                var start = _points[i - 1];
                var end = _points[i + 1];

                Draw(start, end);
            }
        }
    }

    public void Dispose()
    {

    }
}