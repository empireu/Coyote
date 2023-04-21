﻿using System.Text.Json.Serialization;
using Arch.Core.Extensions;
using Coyote.Mathematics;

namespace Coyote.App.Movement;

internal struct JsonTranslationPoint
{
    [JsonInclude]
    public JsonVector2 Position { get; set; }

    [JsonInclude]
    public JsonVector2 Velocity { get; set; }

    [JsonInclude]
    public JsonVector2 Acceleration { get; set; }
}

internal struct JsonRotationPoint
{
    [JsonInclude]
    public JsonVector2 Position { get; set; }

    [JsonInclude]
    public JsonVector2 Heading { get; set; }

    [JsonInclude]
    public float Parameter { get; set; }
}

internal struct JsonMotionConstraints
{
    [JsonInclude]
    public double LinearVelocity { get; set; }
    [JsonInclude]
    public double LinearAcceleration { get; set; }
    [JsonInclude]
    public double AngularVelocity { get; set; }
    [JsonInclude]
    public double AngularAcceleration { get; set; }
    [JsonInclude]
    public double CentripetalAcceleration { get; set; }
}

internal struct JsonGenerationParameters
{
    public float Dx { get; set; }
    public float Dy { get; set; }
    public float DAngleTranslation { get; set; }
    public float DParameterTranslation { get; set; }
    public float DAngleRotation { get; set; }
    public float DParameterRotation { get; set; }
}

internal class MotionProject
{
    [JsonInclude]
    public JsonTranslationPoint[] TranslationPoints { get; set; }

    [JsonInclude]
    public JsonRotationPoint[] RotationPoints { get; set; }

    [JsonInclude]
    public float Scale { get; set; }

    [JsonInclude]
    public JsonMotionConstraints Constraints { get; set; }

    [JsonInclude]
    public JsonGenerationParameters Parameters { get; set; }

    [JsonInclude]
    public int Version { get; set; }

    public void Load(PathEditor editor)
    {
        editor.Clear();
        editor.KnobSensitivity = Scale;

        foreach (var point in TranslationPoints)
        {
            var entity = editor.CreateTranslationPoint(point.Position, false, true);

            ref var component = ref entity.Get<TranslationPointComponent>();

            component.VelocityMarker.Get<PositionComponent>().Position = point.Velocity;
            component.AccelerationMarker.Get<PositionComponent>().Position = point.Acceleration;
        }

        editor.RebuildTranslation();

        foreach (var point in RotationPoints)
        {
            var entity = editor.CreateRotationPoint(point.Position, false);

            ref var component = ref entity.Get<RotationPointComponent>();

            component.HeadingMarker.Get<PositionComponent>().Position = point.Heading;
            component.Parameter = point.Parameter.ToReal<Percentage>();
        }

        editor.RebuildRotationSpline();

        editor.Version = Version;
    }

    public static MotionProject FromPath(PathEditor editor)
    {
        var project = new MotionProject
        {
            TranslationPoints = new JsonTranslationPoint[editor.TranslationPoints.Count],
            RotationPoints = new JsonRotationPoint[editor.RotationPoints.Count]
        };

        for (var i = 0; i < editor.TranslationPoints.Count; i++)
        {
            var entity = editor.TranslationPoints[i];

            project.TranslationPoints[i] = new JsonTranslationPoint
            {
                Position = entity.Get<PositionComponent>().Position,
                Velocity = entity.Get<TranslationPointComponent>().VelocityMarker.Get<PositionComponent>().Position,
                Acceleration = entity.Get<TranslationPointComponent>().AccelerationMarker.Get<PositionComponent>().Position
            };
        }

        var sortedRotationPoints =
            editor.RotationPoints.OrderBy(x => x.Get<RotationPointComponent>().Parameter).ToArray();

        for (var i = 0; i < editor.RotationPoints.Count; i++)
        {
            var entity = sortedRotationPoints[i];

            project.RotationPoints[i] = new JsonRotationPoint
            {
                Position = entity.Get<PositionComponent>().Position,
                Heading = entity.Get<RotationPointComponent>().HeadingMarker.Get<PositionComponent>().Position,
                Parameter = (float)entity.Get<RotationPointComponent>().Parameter.Value
            };
        }

        project.Scale = editor.KnobSensitivity;

        return project;
    }
}