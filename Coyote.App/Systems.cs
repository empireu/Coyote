using Arch.Core;
using Arch.Core.Extensions;
using GameFramework.Extensions;
using GameFramework.Renderer.Batch;

namespace Coyote.App;

internal static class Systems
{
    public static void RenderSprites(World world, QuadBatch batch)
    {
        world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent, SpriteComponent>(), (ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref SpriteComponent spriteComponent) =>
        {
            batch.TexturedQuad(positionComponent.Position, scaleComponent.Scale, spriteComponent.Sprite.Texture);
        });

        world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent, RotationComponent, SpriteComponent>(), (ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref RotationComponent rotationComponent, ref SpriteComponent spriteComponent) =>
        {
            batch.TexturedQuad(positionComponent.Position, scaleComponent.Scale, rotationComponent.Angle, spriteComponent.Sprite.Texture);
        });
    }

    public static void RenderConnections(World world, QuadBatch batch)
    {
        world.Query(new QueryDescription().WithAll<PositionComponent, TranslationPointComponent>(), (ref PositionComponent positionComponent, ref TranslationPointComponent translationPointComponent) =>
        {
            batch.Line(
                positionComponent.Position, 
                translationPointComponent.VelocityMarker.Get<PositionComponent>().Position,
                TranslationPointComponent.VelocityLineColor,
                TranslationPointComponent.VelocityLineThickness);

            batch.Line(
                positionComponent.Position,
                translationPointComponent.AccelerationMarker.Get<PositionComponent>().Position,
                TranslationPointComponent.AccelerationLineColor,
                TranslationPointComponent.AccelerationLineThickness);
        });

        world.Query(new QueryDescription().WithAll<PositionComponent, RotationPointComponent>(), (ref PositionComponent positionComponent, ref RotationPointComponent rotationPointComponent) =>
        {
            batch.Line(
                positionComponent.Position,
                rotationPointComponent.HeadingMarker.Get<PositionComponent>().Position,
                RotationPointComponent.HeadingLineColor,
                RotationPointComponent.HeadingLineThickness);
        });
    }
}