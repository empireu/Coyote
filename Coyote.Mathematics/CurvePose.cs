using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

/// <summary>
///     Represents a pose that is part of a curve. <see cref="Curvature"/> describes the curvature of the curve at this specified pose.
/// </summary>
public readonly struct CurvePose
{
    [JsonInclude]
    public Pose2d Pose { get; }

    [JsonInclude]
    public double Curvature { get; }

    [JsonInclude]
    public double Parameter { get; }

    [JsonConstructor]
    public CurvePose(Pose2d pose, double curvature, double parameter)
    {
        Pose = pose;
        Curvature = curvature;
        Parameter = parameter;
    }

    public override string ToString()
    {
        return $"{Pose} {Curvature} {Parameter}";
    }
}