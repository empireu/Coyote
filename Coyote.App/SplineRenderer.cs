using Coyote.Mathematics;
using GameFramework.Renderer.Batch;
using System.Numerics;
using GameFramework.Extensions;
using Veldrid;

namespace Coyote.App;

public sealed class SplineRenderer
{
    private const int SamplesPerSegment = 64;
    private static readonly RgbaFloat LineColor = new(0.9f, 1f, 1f, 0.9f);
    private const float LineThickness = 0.015f;

    private readonly List<Vector2> _renderPoints = new();

    public void Clear()
    {
        _renderPoints.Clear();
    }

    public void Update(IPositionSpline spline, int segments)
    {
        _renderPoints.Clear();

        var samples = segments * SamplesPerSegment;
        for (var subSampleIndex = 0; subSampleIndex < samples; subSampleIndex++)
        {
            var t0 = subSampleIndex / (samples - 1f);

            _renderPoints.Add(spline.EvaluateTranslation(t0.ToReal<Percentage>()));
        }
    }

    public void Submit(QuadBatch batch)
    {
        if (_renderPoints.Count < 2)
        {
            return;
        }

        for (var i = 1; i < _renderPoints.Count; i++)
        {
            var start = _renderPoints[i - 1];
            var end = _renderPoints[i];

            batch.Line(start, end, LineColor, LineThickness);
        }
    }
}