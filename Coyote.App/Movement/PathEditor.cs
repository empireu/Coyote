using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.Mathematics;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Utilities;
using GameFramework.Utilities.Extensions;

namespace Coyote.App.Movement;

internal sealed class PathEditor : IDisposable
{
    private const float InitialTranslation = 0.1f;
    private const float InitialKnobSize = 0.025f;
    private const float PositionKnobSize = 0.05f;
    private const float RotationKnobSize = 0.035f;
    private const float MarkerSize = 0.1f;
    private const float MarkerYOffset = 0.07f;
    private const float IndicatorSize = 0.025f;
    private const float AddToEndThreshold = 0.05f;

    // Required for robot code:
    public float KnobSensitivity = 5;

    private readonly World _world;

    private readonly List<Entity> _translationPoints = new();
    private readonly List<Entity> _rotationPoints = new();
    private readonly List<Entity> _markerPoints = new();

    public IReadOnlyList<Entity> TranslationPoints => _translationPoints;
    public IReadOnlyList<Entity> RotationPoints => _rotationPoints;
    public IReadOnlyList<Entity> MarkerPoints => _markerPoints;

    private readonly Sprite _positionSprite;
    private readonly Sprite _velocitySprite;
    private readonly Sprite _accelerationSprite;
    private readonly Sprite _markerSprite;

    /// <summary>
    ///     Gets the arc length of the translation path.
    /// </summary>
    public Real<Displacement> ArcLength { get; private set; }

    /// <summary>
    ///     Gets the translation spline.
    /// </summary>
    public QuinticSpline TranslationSpline { get; } = new(2);

    /// <summary>
    ///     Gets the rotation spline. This spline may be empty if no rotation points are specified.
    /// </summary>
    public QuinticSplineMapped RotationSpline { get; } = new(1);

    public int Version;

    private readonly SplineRenderer _pathRenderer = new();

    public PathEditor(GameApplication app, World world)
    {
        _world = world;

        _positionSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.PositionMarker.png"));
        _velocitySprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.VelocityMarker.png"));
        _accelerationSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.AccelerationMarker.png"));
        _markerSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Marker.png"));
    }

    /// <summary>
    ///     Creates a translation point at the specified <see cref="position"/>.
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

            var projection = TranslationSpline.Project(position.ToRealVector<Displacement>());

            // It is close enough to the ends that we can add it there:
            if (projection < AddToEndThreshold)
            {
                _translationPoints.Insert(0, entity);
            }
            else if (projection > 1 - AddToEndThreshold)
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

    public bool CanCreateRotationPoint => TranslationSpline.Segments.Count > 0;

    /// <summary>
    ///     Builds a rotation point close to the specified <see cref="position"/>.
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
        var translationParameter = TranslationSpline.Project(position.ToRealVector<Displacement>());
        
        var projectedPosition = TranslationSpline.Evaluate(translationParameter).ToReal2();

        var headingKnob = CreateDerivativeKnob(0, projectedPosition, RebuildRotationSpline);

        var entity = _world.Create(new PositionComponent
        {
            Position = projectedPosition,
            UpdateCallback = (entity, pos) =>
            {
                OnControlPointChanged(entity, pos, () => { }, headingKnob);

                // Remap position after projection.
                pos = entity.Get<PositionComponent>().Position;

                ReProjectPathElement(entity, param => entity.Get<RotationPointComponent>().Parameter = param);

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

    public bool CanCreateMarker => TranslationSpline.Segments.Count > 0;

    private Pose GetMarkerSpriteTransform(Real<Percentage> parameter)
    {
        var rotation = TranslationSpline
            .EvaluateVelocity(parameter)
            .ToVector2()
            .Exp();

        return new Pose(new Translation(0, MarkerYOffset).Rotated(rotation), rotation);
    }

    private void ProjectMarker(Entity marker)
    {
        ReProjectPathElement(marker, param =>
        {
            marker.Get<MarkerComponent>().Parameter = param;
            marker.Get<SpriteComponent>().Transform = GetMarkerSpriteTransform(param);
        });
    }

    /// <summary>
    ///     Creates a displacement marker close to the specified <see cref="position"/>.
    /// </summary>
    /// <param name="position">
    ///     The world position of the marker.
    ///     This position will be projected onto the translation path and the final position will be the projected position.
    /// </param>
    /// <returns>The new marker entity.</returns>
    public Entity CreateMarker(Vector2 position)
    {
        // Needed because we are not re-building the path (which increments the version in the case of translation and rotation points).
        Version++;

        // These are pretty similar to rotation points. They get projected onto the path and we use them with our node system to 
        // trigger actions on the trajectory.
        var translationParameter = TranslationSpline.Project(position.ToRealVector<Displacement>());
        var projectedPosition = TranslationSpline.Evaluate(translationParameter).ToReal2();

        var entity = _world.Create(new PositionComponent
        {
            Position = projectedPosition,
            UpdateCallback = (entity, _) =>
            {
                ProjectMarker(entity);
            }
        },
        new ScaleComponent { Scale = Vector2.One * MarkerSize },
        new MarkerComponent { Parameter = translationParameter, Name = "Marker" },
        new SpriteComponent
        {
            Sprite = _markerSprite, 
            Transform = GetMarkerSpriteTransform(translationParameter)
        });

        _markerPoints.Add(entity);

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
        _markerPoints.Clear();

        _pathRenderer.Clear();
        ArcLength = Real<Displacement>.Zero;
        RebuildTranslation();
        RebuildRotationSpline();
    }

    public bool IsTranslationPoint(Entity entity)
    {
        return _translationPoints.Contains(entity);
    }

    public bool IsRotationPoint(Entity entity)
    {
        return _rotationPoints.Contains(entity);
    }

    public bool IsMarker(Entity entity)
    {
        return _markerPoints.Contains(entity);
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
    ///     Destroys a marker. This also deletes it from the world.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <exception cref="InvalidOperationException">Thrown if the entity is not a tracked marker.</exception>
    public void DestroyMarker(Entity entity)
    {
        if (!IsMarker(entity))
        {
            throw new InvalidOperationException("Entity is not marker!");
        }

        _world.Destroy(entity);
        _markerPoints.Remove(entity);

        Version++;
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
    ///     Destroys all markers by calling <see cref="DestroyMarker"/>
    /// </summary>
    private void DestroyMarkers()
    {
        var points = _markerPoints.ToArray();

        foreach (var marker in points)
        {
            DestroyMarker(marker);
        }

        Assert.IsTrue(_markerPoints.Count == 0);
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
                .Select(x => x.Get<PositionComponent>().Position)
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
    private static void OnControlPointChanged(Entity entity, Vector2 oldPosition, Action trajectoryCallback, params Entity[] knobs)
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
        Version++;

        TranslationSpline.Clear();
        _pathRenderer.Clear();

        ArcLength = Real<Displacement>.Zero;

        if (_translationPoints.Count < 2)
        {
            DestroyRotationPoints();
            DestroyMarkers();

            OnTranslationChanged?.Invoke();

            return;
        }

        for (var i = 1; i < _translationPoints.Count; i++)
        {
            UnpackTranslation(_translationPoints[i - 1], out var p0, out var v0, out var a0);
            UnpackTranslation(_translationPoints[i], out var p1, out var v1, out var a1);

            TranslationSpline.Add(new QuinticSplineSegment(
                p0.ToRealVector<Displacement>(), 
                v0.ToRealVector<Velocity>(), 
                a0.ToRealVector<Acceleration>(), 
                a1.ToRealVector<Acceleration>(), 
                v1.ToRealVector<Velocity>(), 
                p1.ToRealVector<Displacement>()));
        }

        if (TranslationSpline.Segments.Count > 0)
        {
            _pathRenderer.Update(TranslationSpline);
        }

        ArcLength = TranslationSpline.ComputeArcLength();

        RefitRotationPoints();
        RefitMarkers();

        RebuildRotationSpline();

        OnTranslationChanged?.Invoke();
    }

    /// <summary>
    ///     Refits rotation points using <see cref="ReProjectPathElement"/> and moves the control knobs to updated positions.
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
            var oldPosition = rotationPoint.Get<PositionComponent>().Position;

            ReProjectPathElement(rotationPoint, param => rotationPoint.Get<RotationPointComponent>().Parameter = param);

            // Move knob to new position:
            OnControlPointChanged(rotationPoint, oldPosition, () => { }, rotationPoint.Get<RotationPointComponent>().HeadingMarker);
        }
    }

    /// <summary>
    ///     Refits markers using <see cref="ProjectMarker"/>
    /// </summary>
    private void RefitMarkers()
    {
        if (TranslationSpline.Segments.Count == 0)
        {
            Assert.Fail();
            return;
        }

        foreach (var marker in _markerPoints)
        {
            ProjectMarker(marker);
        }
    }

    /// <summary>
    ///     Projects the path element on a modified translation arc.
    ///     This is done in the following steps:
    ///         - The original position is obtained
    ///         - The position is projected onto the new arc
    ///         - The rotation point's parameter is updated to the re-projected one using <see cref="applyUpdate"/>
    ///         - The rotation point's world position is updated to the re-projected position
    /// </summary>
    private void ReProjectPathElement(Entity point, Action<Real<Percentage>> applyUpdate)
    {
        var position = point.Get<PositionComponent>().Position;
        var parameter = TranslationSpline.Project(position.ToRealVector<Displacement>());

        applyUpdate(parameter);
        point.Get<PositionComponent>().Position = TranslationSpline.Evaluate(parameter).ToVector2();
    }

    /// <summary>
    ///     Re-builds the rotation spline. Two rotation points are needed, and, if that is not met, the spline will be left clear.
    /// </summary>
    public void RebuildRotationSpline()
    {
        Version++;

        RotationSpline.Clear();

        if (_rotationPoints.Count < 2)
        {
            OnRotationChanged?.Invoke();
            return;
        }

        _rotationPoints.Sort((entity1, entity2) =>
            entity1.Get<RotationPointComponent>().Parameter.CompareTo(entity2.Get<RotationPointComponent>().Parameter));

        var builder = new QuinticSplineMappedBuilder(1);

        var previousAngle = Rotation.Zero;

        for (var index = 0; index < _rotationPoints.Count; index++)
        {
            var rotationPoint = _rotationPoints[index];
            UnpackRotation(rotationPoint, out _, out var headingVector, out var parameter);
            var angle = Math.Atan2(headingVector.Y, headingVector.X);

            if (index > 0)
            {
                UnpackRotation(_rotationPoints[index - 1], out _, out _, out var previousParameter);

                // Prevent rare occurrence of equal parameters (e.g. when two points get snapped to the end of the spline)
                if (parameter.ApproxEquals(previousParameter))
                {
                    continue;
                }

                angle = previousAngle + Angles.DeltaAngle(angle, previousAngle);
            }

            previousAngle = (Rotation)angle;

            builder.Add(parameter, angle.ToRealVector<Displacement>());
        }

        builder.Build(RotationSpline);

        OnRotationChanged?.Invoke();
    }

    /// <summary>
    ///     Retrieves translation-specific data from the translation point.
    /// </summary>
    /// <param name="translationPoint">The translation point entity.</param>
    /// <param name="position">The world position of the translation point.</param>
    /// <param name="velocity">The relative velocity vector.</param>
    /// <param name="acceleration">The relative acceleration vector.</param>
    private void UnpackTranslation(Entity translationPoint, out Vector2 position, out Vector2 velocity, out Vector2 acceleration)
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
    private static void UnpackRotation(Entity rotationPoint, out Vector2 position, out Vector2 heading, out float parameter)
    {
        position = rotationPoint.Get<PositionComponent>().Position;
        var markers = rotationPoint.Get<RotationPointComponent>();

        heading = markers.HeadingMarker.Get<PositionComponent>().Position - position;
        parameter = (float)markers.Parameter;
    }

    /// <summary>
    ///     Renders the translation path.
    /// </summary>
    public void SubmitPath(QuadBatch batch)
    {
        _pathRenderer.Submit(batch);
    }

    /// <summary>
    ///     Submits an indicator on the translation path using a projection of <see cref="position"/>.
    /// </summary>
    public void SubmitIndicator(QuadBatch batch, Vector2 position)
    {
        if (TranslationSpline.Segments.Count == 0)
        {
            return;
        }

        batch.TexturedQuad(
            TranslationSpline.Evaluate(
                TranslationSpline
                    .Project(position.ToRealVector<Displacement>()))
                .ToVector2(),
            Vector2.One * IndicatorSize,
            _positionSprite.Texture);
    }

    public event Action? OnTranslationChanged;
    public event Action? OnRotationChanged;

    public void Dispose()
    {
        _pathRenderer.Dispose();
    }
}