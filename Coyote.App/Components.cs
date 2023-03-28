using System.Numerics;
using GameFramework.Renderer;

namespace Coyote.App;

internal struct PositionComponent
{
    public Vector2 Position;

    public Action? UpdateCallback;
}

internal struct ScaleComponent
{
    public Vector2 Scale;
}

internal struct RotationComponent
{
    public float Angle;
}

internal struct EditorComponent
{
    public bool AllowDragging;
}

internal struct SpriteComponent
{
    public Sprite Sprite;
}