using System.Numerics;
using GameFramework.Extensions;
using GameFramework.Renderer.Batch;
using Veldrid;

namespace Coyote.App;

public struct QuinticSplineSegment
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
}

internal class UniformSpline
{
    private const int SubSamples = 128;
    private static readonly RgbaFloat LineColor = new(0.9f, 1f, 1f, 0.9f);
    private const float LineThickness = 0.02f;

    public List<QuinticSplineSegment> Segments { get; } = new();

    private Vector2[] _renderPoints = Array.Empty<Vector2>();

    public void Clear()
    {
        Segments.Clear();
    }

    public void Add(QuinticSplineSegment segment)
    {
        Segments.Add(segment);
    }

    public void UpdateRenderPoints()
    {
        if (Segments.Count == 0)
        {
            return;
        }

        var points = new List<Vector2>();

        foreach (var p in Segments)
        {
            for (var subSampleIndex = 1; subSampleIndex < SubSamples; subSampleIndex++)
            {
                var t0 = (subSampleIndex - 1) / (SubSamples - 1f);

                points.Add(Hermite5(p.P0, p.V0, p.A0, p.A1, p.V1, p.P1, t0));
            }
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

    private static Vector2 Hermite5(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        var t4 = t3 * t;
        var t5 = t4 * t;

        var h0 = 1 - 10 * t3 + 15 * t4 - 6 * t5;
        var h1 = t - 6 * t3 + 8 * t4 - 3 * t5;
        var h2 = 1 / 2f * t2 - 3 / 2f * t3 + 3 / 2f * t4 - 1 / 2f * t5;
        var h3 = 1 / 2f * t3 - t4 + 1 / 2f * t5;
        var h4 = -4 * t3 + 7 * t4 - 3 * t5;
        var h5 = 10 * t3 - 15 * t4 + 6 * t5;

        return h0 * p0 + h1 * v0 + h2 * a0 +
               h3 * a1 + h4 * v1 + h5 * p1;
    }
}