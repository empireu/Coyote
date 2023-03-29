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
    private const float InitialTranslation = 0.1f;
    private const float InitialKnobSize = 0.025f;
    private const float PositionKnobSize = 0.05f;
    private const float IndicatorSize = 0.025f;
    private const float KnobSensitivity = 5;
    private const float AddToEndThreshold = 0.05f;

    private readonly World _world;
    private readonly List<Entity> _translationPoints = new();

    public IReadOnlyList<Entity> TranslationPoints => _translationPoints;

    private readonly Sprite _positionSprite;
    private readonly Sprite _velocitySprite;
    private readonly Sprite _accelerationSprite;

    public float ArcLength { get; private set; }

    public UniformQuinticSpline TranslationSpline { get; } = new();

    public PathEditor(GameApplication app, World world)
    {
        _world = world;

        _positionSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.PositionMarker.png"));
        _velocitySprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.VelocityMarker.png"));
        _accelerationSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.AccelerationMarker.png"));
    }

    public Entity CreateTranslationPoint(Vector2 position, bool rebuildPath = true, bool addToEnd = false)
    {
        var velocityKnob = CreateDerivativeKnob(0, position, RebuildTranslation);
        var accelerationKnob = CreateDerivativeKnob(1, position, RebuildTranslation);

        var entity = _world.Create(
            new PositionComponent 
            {
                Position = position, 
                UpdateCallback = (entity, pos) => OnTranslationPointChanged(entity, pos, RebuildTranslation, velocityKnob, accelerationKnob)
            },
            new ScaleComponent { Scale = Vector2.One * PositionKnobSize },
            new TranslationPointComponent { VelocityMarker = velocityKnob, AccelerationMarker = accelerationKnob },
            new SpriteComponent { Sprite = _positionSprite });

        if (!addToEnd && _translationPoints.Count >= 2)
        {
            var projection = TranslationSpline.Project(position);

            if (projection < AddToEndThreshold)
            {
                _translationPoints.Insert(0, entity);
            }
            else if (projection > (1 - AddToEndThreshold))
            {
                _translationPoints.Add(entity);   
            }
            else
            {
                var closestTwo = _translationPoints.OrderBy(x => Vector2.Distance(x.Get<PositionComponent>().Position, position)).Take(2).ToArray();

                var index = Math.Clamp(
                    (_translationPoints.IndexOf(closestTwo[0]) + _translationPoints.IndexOf(closestTwo[1])) / 2 + 1, 0,
                    _translationPoints.Count);

                _translationPoints.Insert(index, entity);
            }
        }
        else
        {
            _translationPoints.Add(entity);
        }

        if (rebuildPath)
        {
            RebuildTranslation();
        }

        return entity;
    }

    public void Clear()
    {
        _translationPoints.Clear();
        ArcLength = 0;
    }

    public bool IsTranslationPoint(Entity entity)
    {
        return _translationPoints.Contains(entity);
    }

    public void DeleteTranslationPoints(Entity entity)
    {
        if (!IsTranslationPoint(entity))
        {
            throw new InvalidOperationException("Entity is not translation point!");
        }

        var translationPointComponent = entity.Get<TranslationPointComponent>();

        _world.Destroy(translationPointComponent.VelocityMarker);
        _world.Destroy(translationPointComponent.AccelerationMarker);

        _world.Destroy(entity);
        _translationPoints.Remove(entity);

        RebuildTranslation();
    }

    private Entity CreateDerivativeKnob(int derivative, Vector2 initialPosition, Action changeCallback)
    {
        var sprite = derivative switch
        {
            0 => _velocitySprite,
            1 => _accelerationSprite,
            _ => throw new ArgumentOutOfRangeException($"Unknown derivative {derivative}")
        };

        var direction = Vector2.One;

        if (_translationPoints.Count == 1)
        {
            var closest = _translationPoints
                .MinBy(x => Vector2.Distance(x.Get<PositionComponent>().Position, initialPosition))
                .Get<PositionComponent>().Position;

            if (Vector2.DistanceSquared(closest, initialPosition) > 0.001)
            {
                // Prevents NaN and infinity

                direction = Vector2.Normalize(initialPosition - closest);
            }
        }
        else if (_translationPoints.Count >= 2)
        {
            var points = _translationPoints
                .OrderBy(x => Vector2.Distance(x.Get<PositionComponent>().Position, initialPosition))
                .Select(x=>x.Get<PositionComponent>().Position)
                .Take(2)
                .ToArray();

            if (Vector2.DistanceSquared(points[0], points[1]) > 0.001)
            {
                direction = Vector2.Normalize(points[0] - points[1]);
            }
        }

        var position = initialPosition + InitialTranslation * direction * (1 + derivative);
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

    public void RebuildTranslation()
    {
        TranslationSpline.Clear();
        ArcLength = 0;

        if (_translationPoints.Count < 2)
        {
            return;
        }

        for (var i = 1; i < _translationPoints.Count; i++)
        {
            UnpackTranslation(_translationPoints[i - 1], out var p0, out var v0, out var a0);
            UnpackTranslation(_translationPoints[i], out var p1, out var v1, out var a1);

            TranslationSpline.Add(new QuinticSplineSegment(p0, v0, a0, a1, v1, p1));
        }

        TranslationSpline.UpdateRenderPoints();

        ArcLength = TranslationSpline.ComputeArcLength();
    }

    public static void UnpackTranslation(Entity translationPoint, out Vector2 position, out Vector2 velocity, out Vector2 acceleration)
    {
        position = translationPoint.Get<PositionComponent>().Position;
        var markers = translationPoint.Get<TranslationPointComponent>();

        velocity = markers.VelocityMarker.Get<PositionComponent>().Position - position;
        acceleration = markers.AccelerationMarker.Get<PositionComponent>().Position - position;

        velocity *= KnobSensitivity;
        acceleration *= KnobSensitivity;
    }

    public void DrawTranslationPath(QuadBatch batch, Func<Vector2, Vector2>? mapping = null)
    {
        TranslationSpline.Render(batch, mapping);
    }

    public void DrawIndicator(QuadBatch batch, Vector2 position)
    {
        if (TranslationSpline.Segments.Count == 0)
        {
            return;
        }

        batch.TexturedQuad(
            TranslationSpline.Evaluate(TranslationSpline.Project(position)),
            Vector2.One * IndicatorSize, 
            _positionSprite.Texture);
    }
}