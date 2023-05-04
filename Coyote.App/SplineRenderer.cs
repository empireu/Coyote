using Coyote.Mathematics;
using GameFramework.Renderer.Batch;
using GameFramework.Extensions;
using Veldrid;

namespace Coyote.App;

public sealed class SplineRenderer : IDisposable
{
    private static readonly RgbaFloat LineColor = new(0.9f, 1f, 1f, 0.9f);
    private const float LineThickness = 0.015f;
    private static readonly Twist AdmissibleTwist = new(0.1, 0.1, Math.PI / 16);
    private const int MaxIterations = 8192;

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

        for (var i = 1; i < _points.Count; i++)
        {
            var start = _points[i - 1];
            var end = _points[i];

            batch.Line(start.Pose.Translation, end.Pose.Translation, LineColor, LineThickness);
        }
    }

    public void Dispose()
    {

    }
}