using System.Numerics;
using Arch.Core;
using Coyote.Mathematics;
using GameFramework.Renderer;

namespace Coyote.App;

internal struct PositionComponent
{
    public delegate void UpdateDelegate(Entity entity, Vector2 oldPosition);

    [FloatEditor(-3.66f, 3.66f)]
    public Vector2 Position;

    // old position
    public UpdateDelegate? UpdateCallback;
}

internal struct ScaleComponent
{
    public Vector2 Scale;
}

internal struct RotationComponent
{
    public float Angle;
}

internal struct SpriteComponent
{
    public Sprite Sprite;
    public bool Disabled;
}

internal struct TranslationPointComponent
{
    public static readonly Vector4 VelocityLineColor = new(1, 0, 0, 0.9f);
    public static readonly Vector4 AccelerationLineColor = new(1, 1, 0, 0.9f);

    public const float VelocityLineThickness = 0.01f;
    public const float AccelerationLineThickness = 0.008f;

    public Entity VelocityMarker;
    public Entity AccelerationMarker;
}

internal struct RotationPointComponent
{
    public static readonly Vector4 HeadingLineColor = new(0, 1, 0, 0.9f);

    public const float HeadingLineThickness = 0.01f;

    public Entity HeadingMarker;
    public Real<Percentage> Parameter;
}