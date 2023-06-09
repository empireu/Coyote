﻿using System.Numerics;
using Arch.Core;
using Coyote.Mathematics;
using GameFramework.Renderer;

namespace Coyote.App;

internal struct PositionComponent
{
    public delegate void UpdateDelegate(Entity entity, Vector2 oldPosition);
    public delegate Vector2 ControlledMoveDelegate(Entity entity, Vector2 targetPos, GameFramework.GameInput input);

    [FloatEditor(-3.66f, 3.66f)]
    public Vector2 Position;

    /// <summary>
    ///     Called before a position is applied, to calculate a position based on user actions.
    /// </summary>
    public ControlledMoveDelegate? ControlledMoveCallback;

    /// <summary>
    ///     Called when a position is applied.
    /// </summary>
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
    public Pose2d? Transform;
    public bool Disabled;
}

internal struct KnobComponent
{
    public Entity Parent;
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
    public double Parameter;
}

internal struct MarkerComponent
{
    public double Parameter;

    [StringEditor]
    public string Name;
}