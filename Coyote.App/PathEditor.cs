using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using Veldrid;

namespace Coyote.App;

internal sealed class PathEditor
{
    private const float InitialTranslation = 0.025f;
    private const float InitialKnobSize = 0.025f;
    private const float PositionKnobSize = 0.05f;
    private const float IndicatorSize = 0.025f;
    private const float KnobSensitivity = 5;

    private readonly World _world;
    private readonly List<Entity> _translationPoints = new();

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

    public void CreateTranslationPoint(Vector2 position)
    {
        var velocityKnob = CreateDerivativeKnob(0, position, OnTranslationChanged);
        var accelerationKnob = CreateDerivativeKnob(1, position, OnTranslationChanged);

        var entity = _world.Create(
            new PositionComponent 
            {
                Position = position, 
                UpdateCallback = (entity, pos) => OnTranslationPointChanged(entity, pos, OnTranslationChanged, velocityKnob, accelerationKnob)
            },
            new ScaleComponent { Scale = Vector2.One * PositionKnobSize },
            new TranslationPointComponent { VelocityMarker = velocityKnob, AccelerationMarker = accelerationKnob },
            new SpriteComponent { Sprite = _positionSprite });

        _translationPoints.Add(entity);

        OnTranslationChanged();
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

        OnTranslationChanged();
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

    private static void UnpackTranslation(Entity translationPoint, out Vector2 position, out Vector2 velocity, out Vector2 acceleration)
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