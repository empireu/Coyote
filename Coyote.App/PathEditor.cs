using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Utilities;
using Vortice.Mathematics;

namespace Coyote.App;

internal sealed class PathEditor
{
    private const float InitialTranslation = 0.1f;
    private const float InitialKnobSize = 0.025f;
    private const float PositionKnobSize = 0.05f;
    private const float RotationKnobSize = 0.035f;
    private const float IndicatorSize = 0.025f;
    private const float KnobSensitivity = 5;
    private const float AddToEndThreshold = 0.05f;

    private readonly World _world;
    
    private readonly List<Entity> _translationPoints = new();
    private readonly List<Entity> _rotationPoints = new();

    public IReadOnlyList<Entity> TranslationPoints => _translationPoints;
    public IReadOnlyList<Entity> RotationPoints => _rotationPoints;

    private readonly Sprite _positionSprite;
    private readonly Sprite _velocitySprite;
    private readonly Sprite _accelerationSprite;

    /// <summary>
    ///     Gets the arc length of the translation path.
    /// </summary>
    public float ArcLength { get; private set; }

    /// <summary>
    ///     Gets the translation spline.
    /// </summary>
    public UniformQuinticSpline TranslationSpline { get; } = new();

    /// <summary>
    ///     Gets the rotation spline. This spline may be empty if no rotation points are specified.
    /// </summary>
    public MappedCubicSpline RotationSpline { get; } = new();

    public PathEditor(GameApplication app, World world)
    {
        _world = world;

        _positionSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.PositionMarker.png"));
        _velocitySprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.VelocityMarker.png"));
        _accelerationSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.AccelerationMarker.png"));
    }

    /// <summary>
    ///     Creates a translation point at the specified position.
    /// </summary>
    /// <param name="position">The world position of the translation point.</param>
    /// <param name="rebuildPath">If true, the path will be re-built. If bulk creation is wanted, setting this to false can reduce extraneous computation.</param>
    /// <param name="addToEnd">If true, this translation point will be added to the end of the path, regardless of its projected position.</param>
    /// <returns>The new translation point entity.</returns>
    public Entity CreateTranslationPoint(Vector2 position, bool rebuildPath = true, bool addToEnd = false)
    {
        var velocityKnob = CreateDerivativeKnob(0, position, RebuildTranslation);
        var accelerationKnob = CreateDerivativeKnob(1, position, RebuildTranslation);

        var entity = _world.Create(
            new PositionComponent
            {
                Position = position,
                UpdateCallback = (entity, pos) => OnControlPointChanged(entity, pos, RebuildTranslation, velocityKnob, accelerationKnob)
            },
            new ScaleComponent { Scale = Vector2.One * PositionKnobSize },
            new TranslationPointComponent { VelocityMarker = velocityKnob, AccelerationMarker = accelerationKnob },
            new SpriteComponent { Sprite = _positionSprite });

        if (!addToEnd && _translationPoints.Count >= 2)
        {
            // We find the best position on the spline to insert this.

            var projection = TranslationSpline.Project(position);

            // It is close enough to the ends that we can add it there:
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
                // Place between two other points.
                var closestTwo = _translationPoints
                    .OrderBy(x => Vector2.Distance(x.Get<PositionComponent>().Position, position)).Take(2).ToArray();

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

    /// <summary>
    ///     Builds a rotation point close to the specified position.
    /// </summary>
    /// <param name="position">
    ///     The world position of the rotation point.
    ///     This position will be projected onto the translation path and the final position will be the projected position.</param>
    /// <param name="rebuildPath">If true, the path will be re-built. If bulk creation is wanted, setting this to false can reduce extraneous computation.</param>
    /// <returns>The new rotation point entity.</returns>
    public Entity CreateRotationPoint(Vector2 position, bool rebuildPath = true)
    {
        // Basically, rotation points are parameterized by translation. So we project this on the path to get the parameter
        // and then create the entity at that position on the arc.
        var translationParameter = TranslationSpline.Project(position);
        var projectedPosition = TranslationSpline.Evaluate(translationParameter);

        var headingKnob = CreateDerivativeKnob(0, projectedPosition, RebuildRotationSpline);

        var entity = _world.Create(new PositionComponent
            {
                Position = projectedPosition,
                UpdateCallback = (entity, pos) =>
                {
                    OnControlPointChanged(entity, pos, () => {}, headingKnob);

                    // Remap position after projection.
                    pos = entity.Get<PositionComponent>().Position;

                    ReProjectRotationPoint(entity);

                    // Also move the knob to the re-projected position and re-build the spline.
                    OnControlPointChanged(entity, pos, RebuildRotationSpline, headingKnob);
                }
            },
            new ScaleComponent { Scale = Vector2.One * RotationKnobSize },
            new RotationPointComponent { HeadingMarker = headingKnob, Parameter = translationParameter },
            new SpriteComponent { Sprite = _velocitySprite });

        _rotationPoints.Add(entity);

        if (rebuildPath)
        {
            RebuildRotationSpline();
        }

        return entity;
    }

    /// <summary>
    ///     Clears all tracked points.
    ///     This will not delete anything from the <see cref="World"/>.
    /// </summary>
    public void Clear()
    {
        _translationPoints.Clear();
        _rotationPoints.Clear();
        ArcLength = 0;
    }

    public bool IsTranslationPoint(Entity entity)
    {
        return _translationPoints.Contains(entity);
    }

    public bool IsRotationPoint(Entity entity)
    {
        return _rotationPoints.Contains(entity);
    }

    /// <summary>
    ///     Destroys a translation point. This also deletes it from the world and removes all knobs.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <exception cref="InvalidOperationException">Thrown if the entity is not a tracked translation point.</exception>
    public void DestroyTranslationPoint(Entity entity)
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

    /// <summary>
    ///     Destroys a rotation point. This also deletes it from the world and removes all knobs.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <exception cref="InvalidOperationException">Thrown if the entity is not a tracked rotation point.</exception>
    public void DestroyRotationPoint(Entity entity)
    {
        if (!IsRotationPoint(entity))
        {
            throw new InvalidOperationException("Entity is not rotation point!");
        }

        var rotationPointComponent = entity.Get<RotationPointComponent>();

        _world.Destroy(rotationPointComponent.HeadingMarker);

        _world.Destroy(entity);
        _rotationPoints.Remove(entity);

        RebuildRotationSpline();
    }

    /// <summary>
    ///     Destroys all rotation points by calling <see cref="DestroyRotationPoint"/>.
    /// </summary>
    private void DestroyRotationPoints()
    {
        var points = _rotationPoints.ToArray();
       
        foreach (var rotationPoint in points)
        {
            DestroyRotationPoint(rotationPoint);
        }

        Assert.IsTrue(_rotationPoints.Count == 0);
    }

    /// <summary>
    ///     Creates a manipulable control knob (marker) for the specified derivative.
    /// </summary>
    /// <param name="derivative">The derivative order to use. This will be used to select the rendering options.</param>
    /// <param name="initialPosition">The position of the parent entity.</param>
    /// <param name="changeCallback">A handler for changes in the position of the knob.</param>
    /// <returns>The new knob entity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if no rendering options are available for the specified derivative.</exception>
    private Entity CreateDerivativeKnob(int derivative, Vector2 initialPosition, Action changeCallback)
    {
        var sprite = derivative switch
        {
            0 => _velocitySprite,
            1 => _accelerationSprite,
            _ => throw new ArgumentOutOfRangeException($"Unknown derivative {derivative}")
        };

        var direction = Vector2.One;

        // Get a more useful direction towards some existing points.

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

    /// <summary>
    ///     Called when a parent control point is moved.
    /// </summary>
    /// <param name="entity">The parent entity that was moved.</param>
    /// <param name="oldPosition">The old position of the entity.</param>
    /// <param name="trajectoryCallback">Callback for trajectory updates, if needed.</param>
    /// <param name="knobs">The child knobs. They will be displaced to match the new position of the parent.</param>
    private void OnControlPointChanged(Entity entity, Vector2 oldPosition, Action trajectoryCallback, params Entity[] knobs)
    {
        var displacement = entity.Get<PositionComponent>().Position - oldPosition;

        foreach (var knob in knobs)
        {
            knob.Get<PositionComponent>().Position += displacement;
        }

        trajectoryCallback();
    }

    /// <summary>
    ///     Re-builds the translation spline and re-fits the rotation points on it.
    /// </summary>
    public void RebuildTranslation()
    {
        TranslationSpline.Clear();
        ArcLength = 0;

        if (_translationPoints.Count < 2)
        {
            DestroyRotationPoints();

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

        RefitRotationPoints();
        RebuildRotationSpline();
    }

    /// <summary>
    ///     Re-fits the rotation points on a modified translation arc.
    ///     This is done in the following steps:
    ///         - The original position is obtained
    ///         - The position is projected onto the new arc
    ///         - The rotation point's <see cref="RotationPointComponent.Parameter"/> is updated to the re-projected one.
    ///         - The rotation point's world position is updated to the re-projected position.
    ///         - Knobs are updated with the displacement.
    ///
    ///     Because multiple projections are needed, this is an expensive operation.
    /// </summary>
    private void RefitRotationPoints()
    {
        if (TranslationSpline.Segments.Count == 0)
        {
            Assert.Fail();
            return;
        }

        foreach (var rotationPoint in _rotationPoints)
        {
            ref var position = ref rotationPoint.Get<PositionComponent>().Position;
            var oldPosition = position;

            ref var component = ref rotationPoint.Get<RotationPointComponent>();

            // Refit rotation point on arc (assuming the arc was edited):
            component.Parameter = TranslationSpline.Project(position);

            // Remove projection errors by updating the position again:
            position = TranslationSpline.Evaluate(component.Parameter);

            // Move knob to new position:
            OnControlPointChanged(rotationPoint, oldPosition, () => { }, component.HeadingMarker);
        }
    }

    /// <summary>
    ///     Re-projects the position of the rotation point on the spline. The parameter and world position are updated.
    /// </summary>
    /// <param name="point"></param>
    private void ReProjectRotationPoint(Entity point)
    {
        var position = point.Get<PositionComponent>().Position;
        var parameter = TranslationSpline.Project(position);

        point.Get<RotationPointComponent>().Parameter = parameter;
        point.Get<PositionComponent>().Position = TranslationSpline.Evaluate(parameter);
    }

    /// <summary>
    ///     Re-builds the rotation spline. Two rotation points are needed, and, if that is not met, the spline will be left clear.
    /// </summary>
    public void RebuildRotationSpline()
    {
        RotationSpline.Clear();

        if (_rotationPoints.Count < 2)
        {
            return;
        }

        _rotationPoints.Sort((entity1, entity2) =>
            entity1.Get<RotationPointComponent>().Parameter.CompareTo(entity2.Get<RotationPointComponent>().Parameter));

        var builder = new MappedCubicSplineBuilder();

        var previousAngle = 0f;

        for (var index = 0; index < _rotationPoints.Count; index++)
        {
            var rotationPoint = _rotationPoints[index];
            UnpackRotation(rotationPoint, out _, out var headingVector, out var parameter);
            var angle = MathF.Atan2(headingVector.Y, headingVector.X);

            if (index > 0)
            {
                UnpackRotation(_rotationPoints[index - 1], out _, out _, out var previousParameter);

                // Prevent rare occurrence of equal parameters (e.g. when two points get snapped to the end of the spline)
                if (parameter.ApproxEquals(previousParameter))
                {
                    continue;
                }

                angle = previousAngle + Mathematics.DeltaAngle(angle, previousAngle);
            }

            previousAngle = angle;

            builder.Add(parameter, angle);
        }

        builder.Build(RotationSpline);
    }

    public static float Closest(float a, float b)
    {
        var dir = b % MathF.Tau - a % MathF.Tau;

        if (MathF.Abs(dir) > MathF.PI)
        {
            dir = -(MathF.Sign(dir) * MathF.Tau) + dir;
        }

        return dir;
    }


    /// <summary>
    ///     Retrieves translation-specific data from the translation point.
    /// </summary>
    /// <param name="translationPoint">The translation point entity.</param>
    /// <param name="position">The world position of the translation point.</param>
    /// <param name="velocity">The relative velocity vector.</param>
    /// <param name="acceleration">The relative acceleration vector.</param>
    public static void UnpackTranslation(Entity translationPoint, out Vector2 position, out Vector2 velocity, out Vector2 acceleration)
    {
        position = translationPoint.Get<PositionComponent>().Position;
        var markers = translationPoint.Get<TranslationPointComponent>();

        velocity = markers.VelocityMarker.Get<PositionComponent>().Position - position;
        acceleration = markers.AccelerationMarker.Get<PositionComponent>().Position - position;

        velocity *= KnobSensitivity;
        acceleration *= KnobSensitivity;
    }

    /// <summary>
    ///     Retrieves rotation-specific data from the rotation point.
    /// </summary>
    /// <param name="rotationPoint">The rotation point entity.</param>
    /// <param name="position">The world position of the rotation point.</param>
    /// <param name="heading">The relative heading vector.</param>
    /// <param name="parameter">The translation parameter of the rotation point.</param>
    public static void UnpackRotation(Entity rotationPoint, out Vector2 position, out Vector2 heading, out float parameter)
    {
        position = rotationPoint.Get<PositionComponent>().Position;
        var markers = rotationPoint.Get<RotationPointComponent>();

        heading = markers.HeadingMarker.Get<PositionComponent>().Position - position;
        parameter = markers.Parameter;
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