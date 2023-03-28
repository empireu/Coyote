using Arch.Core;
using GameFramework.Extensions;
using GameFramework.Renderer.Batch;

namespace Coyote.App;

internal static class Systems
{
    public static void Render(World world, QuadBatch batch)
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
}