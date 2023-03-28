using System;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;

namespace Coyote.App;

internal sealed class PathEditor
{
    private const float InitialTranslation = 0.025f;
    private const float InitialKnobSize = 0.025f;
    private const float PositionKnobSize = 0.05f;
    private const float KnobSensitivity = 5;

    private const int Segments = 128;
    private const float LineThickness = 0.025f;

    private readonly World _world;
    private readonly List<Entity> _translationPoints = new();

    private readonly Sprite _positionSprite;
    private readonly Sprite _velocitySprite;
    private readonly Sprite _accelerationSprite;

    public PathEditor(GameApplication app, World world)
    {
        _world = world;

        _positionSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.PositionMarker.png"));
        _velocitySprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.VelocityMarker.png"));
        _accelerationSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.AccelerationMarker.png"));
    }

    private void CreatePathPoint(List<Entity> set, Func<PathLinkComponent, Entity> factory)
    {
        var previousEntity = _translationPoints.LastOrDefault(Entity.Null);

        var entity = factory(new PathLinkComponent
        {
            Previous = previousEntity,
            Next = Entity.Null
        });

        set.Add(entity);

        if (previousEntity.IsAlive())
        {
            previousEntity.Get<PathLinkComponent>().Next = entity;
        }
    }

    public void CreateTranslationPoint(Vector2 position)
    {
        var velocityKnob = CreateDerivativeKnob(0, position, OnTranslationChanged);
        var accelerationKnob = CreateDerivativeKnob(1, position, OnTranslationChanged);

        CreatePathPoint(_translationPoints, pathLink => _world.Create(
            new PositionComponent 
            {
                Position = position, 
                UpdateCallback = (entity, pos) => OnTranslationPointChanged(entity, pos, OnTranslationChanged, velocityKnob, accelerationKnob)
            },
            new ScaleComponent { Scale = Vector2.One * PositionKnobSize },
            new TranslationPointComponent { VelocityMarker = velocityKnob, AccelerationMarker = accelerationKnob },
            new SpriteComponent { Sprite = _positionSprite },
            pathLink));
    }

    private Entity CreateDerivativeKnob(int derivative, Vector2 initialPosition, Action changeCallback)
    {
        var sprite = derivative switch
        {
            0 => _velocitySprite,
            1 => _accelerationSprite,
            _ => throw new ArgumentOutOfRangeException($"Unknown derivative {derivative}")
        };

        var position = initialPosition + InitialTranslation * Vector2.One * (1 + derivative);
        var scale = InitialKnobSize * Vector2.One / (1 + derivative);

        var entity = _world.Create(
            new PositionComponent { Position = position, UpdateCallback = (_, _) => changeCallback() },
            new ScaleComponent { Scale = scale },
            new SpriteComponent { Sprite = sprite });

        return entity;
    }

    private void OnTranslationPointChanged(Entity entity, Vector2 oldPosition, Action trajectoryCallback, params Entity[] knobs)
    {
        var displacement = entity.Get<PositionComponent>().Position - oldPosition;

        foreach (var knob in knobs)
        {
            knob.Get<PositionComponent>().Position += displacement;
        }

        trajectoryCallback();
    }

    private void OnTranslationChanged()
    {
        Console.WriteLine("Translation changed");
    }

    private static void UnpackTranslation(Entity translationPoint, out Vector2 position, out Vector2 velocity, out Vector2 acceleration)
    {
        position = translationPoint.Get<PositionComponent>().Position;
        var markers = translationPoint.Get<TranslationPointComponent>();

        velocity = markers.VelocityMarker.Get<PositionComponent>().Position - position;
        acceleration = markers.AccelerationMarker.Get<PositionComponent>().Position - position;

        velocity *= KnobSensitivity;
        acceleration *= KnobSensitivity;
    }

    public void DrawPaths(QuadBatch batch)
    {
        if (_translationPoints.Count < 2)
        {
            return;
        }

        for (var pointIndex = 1; pointIndex < _translationPoints.Count; pointIndex++)
        {
            UnpackTranslation(_translationPoints[pointIndex - 1], out var p0, out var v0, out var a0);
            UnpackTranslation(_translationPoints[pointIndex], out var p1, out var v1, out var a1);

            for (var segmentIndex = 1; segmentIndex < Segments; segmentIndex++)
            {
                var t0 = (segmentIndex - 1) / (Segments - 1f);
                var t1 = segmentIndex / (Segments - 1f);

                batch.Line(
                    Hermite5(p0, v0, a0, a1, v1, p1, t0),
                    Hermite5(p0, v0, a0, a1, v1, p1, t1),
                    new RgbaFloat4(0.9f, 0.9f, 1f, 0.9f), LineThickness);
            }
        }
    }

    private static Vector2 Hermite5(Vector2 p0, Vector2 v0, Vector2 a0, Vector2 a1, Vector2 v1, Vector2 p1, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        var t4 = t3 * t;
        var t5 = t4 * t;

        var h0 = 1 - 10 * t3 + 15 * t4 - 6 * t5;
        var h1 = t - 6 * t3 + 8 * t4 - 3 * t5;
        var h2 = 1 / 2f * t2 - 3 / 2f * t3 + 3 / 2f * t4 - 1 / 2f * t5;
        var h3 = 1 / 2f * t3 - t4 + 1 / 2f * t5;
        var h4 = -4 * t3 + 7 * t4 - 3 * t5;
        var h5 = 10 * t3 - 15 * t4 + 6 * t5;

        return h0 * p0 + h1 * v0 + h2 * a0 +
               h3 * a1 + h4 * v1 + h5 * p1;
    }
}