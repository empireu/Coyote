using System.Numerics;
using System.Text.Json.Serialization;
using Arch.Core;
using Arch.Core.Extensions;

namespace Coyote.App;

internal struct JsonTranslationPoint
{
    [JsonInclude]
    public JsonVector2 Position { get; set; }

    [JsonInclude]
    public JsonVector2 Velocity { get; set; }

    [JsonInclude]
    public JsonVector2 Acceleration { get; set; }
}

internal class MotionProject
{
    [JsonInclude]
    public JsonTranslationPoint[] TranslationPoints { get; set; }

    public void Load(PathEditor editor)
    {
        editor.Clear();

        foreach (var point in TranslationPoints)
        {
            var entity = editor.CreateTranslationPoint(point.Position, false);
            
            ref var component = ref entity.Get<TranslationPointComponent>();

            component.VelocityMarker.Move(point.Velocity);
            component.AccelerationMarker.Move(point.Acceleration);
        }

        editor.RebuildTranslation();
    }

    public static MotionProject FromPath(PathEditor editor)
    {
        var project = new MotionProject
        {
            TranslationPoints = new JsonTranslationPoint[editor.TranslationPoints.Count]
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

        return project;
    }
}