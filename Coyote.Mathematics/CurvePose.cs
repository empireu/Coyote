using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

/// <summary>
///     Represents a pose that is part of a curve. <see cref="Curvature"/> describes the curvature of the curve at this specified pose.
/// </summary>
public readonly struct CurvePose
{
    [JsonInclude]
    public Pose Pose { get; }

    [JsonInclude]
    public Real<Curvature> Curvature { get; }

    [JsonConstructor]
    public CurvePose(Pose pose, Real<Curvature> curvature)
    {
        Pose = pose;
        Curvature = curvature;
    }
}