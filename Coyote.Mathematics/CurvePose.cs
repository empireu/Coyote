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

    [JsonInclude]
    public Real<Percentage> Parameter { get; }

    [JsonConstructor]
    public CurvePose(Pose pose, Real<Curvature> curvature, Real<Percentage> parameter)
    {
        Pose = pose;
        Curvature = curvature;
        Parameter = parameter;
    }
}