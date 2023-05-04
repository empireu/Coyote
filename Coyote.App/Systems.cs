using System.Numerics;
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
            if (!spriteComponent.Disabled)
            {
                if (spriteComponent.Transform.HasValue)
                {
                    var transform = spriteComponent.Transform.Value;
                    batch.TexturedQuad(
                        positionComponent.Position + ((Vector2)transform.Translation), 
                        scaleComponent.Scale,
                        (float)transform.Rotation.Log(), 
                        spriteComponent.Sprite.Texture);
                }
                else
                {
                    batch.TexturedQuad(positionComponent.Position, scaleComponent.Scale, spriteComponent.Sprite.Texture);
                }
            }
        });

        world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent, RotationComponent, SpriteComponent>(), (ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref RotationComponent rotationComponent, ref SpriteComponent spriteComponent) =>
        {
            if (!spriteComponent.Disabled)
            {
                batch.TexturedQuad(positionComponent.Position, scaleComponent.Scale, rotationComponent.Angle, spriteComponent.Sprite.Texture);
            }
        });
    }

    public static void RenderConnections(World world, QuadBatch batch, bool translationPoints, bool rotationPoints, bool translationVelocity, bool translationAcceleration, bool rotationTangents)
    {
        if (translationPoints)
        {
            world.Query(new QueryDescription().WithAll<PositionComponent, TranslationPointComponent>(), (ref PositionComponent positionComponent, ref TranslationPointComponent translationPointComponent) =>
            {
                if (translationVelocity)
                {
                    batch.Line(
                        positionComponent.Position,
                        translationPointComponent.VelocityMarker.Get<PositionComponent>().Position,
                        TranslationPointComponent.VelocityLineColor,
                        TranslationPointComponent.VelocityLineThickness);
                }

                if (translationAcceleration)
                {
                    batch.Line(
                        positionComponent.Position,
                        translationPointComponent.AccelerationMarker.Get<PositionComponent>().Position,
                        TranslationPointComponent.AccelerationLineColor,
                        TranslationPointComponent.AccelerationLineThickness);
                }
            });
        }

        if (rotationPoints)
        {
            world.Query(new QueryDescription().WithAll<PositionComponent, RotationPointComponent>(), (ref PositionComponent positionComponent, ref RotationPointComponent rotationPointComponent) =>
            {
                if (rotationTangents)
                {
                    batch.Line(
                        positionComponent.Position,
                        rotationPointComponent.HeadingMarker.Get<PositionComponent>().Position,
                        RotationPointComponent.HeadingLineColor,
                        RotationPointComponent.HeadingLineThickness);
                }
            });
        }
    }
}