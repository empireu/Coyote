using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Arch.Core.Extensions;
using Coyote.Mathematics;
using GameFramework.Utilities;

namespace Coyote.App.Movement;

internal sealed class Simulator : IDisposable
{
    /// <summary>
    ///     Controls how often the trajectory is updated when editing.
    /// </summary>
    private const double EditTimeRefreshThreshold = 1.0;

    private readonly App _app;
    private readonly PathEditor _editor;
    private readonly Stopwatch _editTime = Stopwatch.StartNew();

    public Trajectory? Trajectory { get; private set; }
    public Exception? GenerateError { get; private set; }

    public Simulator(App app, PathEditor editor)
    {
        _app = app;
        _editor = editor;

        _editor.OnTranslationChanged += OnPathChanged;
        _editor.OnRotationChanged += OnPathChanged;
        _editor.OnMarkerChanged += OnPathChanged;
    }

    private void OnPathChanged()
    {
        InvalidateTrajectory();
    }

    public void InvalidateTrajectory()
    {
        Trajectory = null;
        GenerateError = null;
        _editTime.Restart();
    }

    public float PlayTime;
    public TrajectoryPoint Last { get; private set; }
    public float TotalTime { get; private set; }
    public float TotalLength { get; private set; }

    public float Speed = 1;

    public double MaxProfileVelocity { get; private set; }
    public double MaxProfileAcceleration { get; private set; }
    public double MaxProfileAngularVelocity { get; private set; }
    public double MaxProfileAngularAcceleration { get; private set; }
    public int Points { get; private set; }

    public Twist2dIncr PathIncr = new(0.001, 0.001, Angles.ToRadians(0.5));
    public double PathParamIncr = 0.001;
    public double SplineRotIncr = Angles.ToRadians(0.5);
    public double RotParamIncr = 0.001;

    public double MaxLinearVelocity = 1.5;
    public double MaxLinearAcceleration = 1.25;
    public double MaxLinearDeacceleration = 1;
    public double MaxCentripetalAcceleration = 0.5;
    public double MaxAngularVelocity = Angles.ToRadians(180);
    public double MaxAngularAcceleration = Angles.ToRadians(100);

    public BaseTrajectoryConstraints Constraints => new(
        MaxLinearVelocity,
        MaxLinearAcceleration,
        MaxLinearDeacceleration,
        MaxAngularVelocity,
        MaxAngularAcceleration,
        MaxCentripetalAcceleration);

    public readonly struct Marker
    {
        public double Parameter { get; }
        public string Label { get; }

        public Marker(double parameter, string label)
        {
            Parameter = parameter;
            Label = label;
        }
    }

    public readonly struct MarkerEvent
    {
        public MarkerEvent(Marker marker, double hitTime)
        {
            Marker = marker;
            HitTime = hitTime;
        }

        public Marker Marker { get; }
        public double HitTime { get; }
    }

    private readonly List<MarkerEvent> _markerEvents = new();
    public IEnumerable<MarkerEvent> MarkerEvents => _markerEvents;

    private readonly List<Marker> _markers = new();

    public bool Generate()
    {
        if (_editor.TranslationSpline.Segments.Count == 0)
        {
            return false;
        }

        GenerateError = null;
        _markerEvents.Clear();
        _markers.Clear();

        PlayTime = 0;

        var poses = new List<CurvePose>();

        var getPointsTime = Measurements.MeasureTimeSpan(() =>
        {
            Splines.GetPoints(poses,
                _editor.TranslationSpline,
                0.0,
                1.0,
                PathParamIncr,
                PathIncr,
                int.MaxValue,
                (t0, t1) =>
                {
                    if ((t0 - t1).Abs() > RotParamIncr)
                    {
                        return true;
                    }

                    if (_editor.RotationSpline.IsEmpty)
                    {
                        return false;
                    }

                    var displacement = Math.Abs(
                        _editor.RotationSpline.Evaluate(t0)[0] - 
                        _editor.RotationSpline.Evaluate(t1)[0]);

                    return displacement > SplineRotIncr;
                }
            );
        });

        if (!_editor.RotationSpline.IsEmpty)
        {
            var span = CollectionsMarshal.AsSpan(poses);

            // Assign spline-spline rotations:
            for (var i = 0; i < poses.Count; i++)
            {
                var point = span[i];

                span[i] = new CurvePose(
                    new Pose2d(point.Pose.Translation, Rotation2d.Exp(_editor.RotationSpline.Evaluate(point.Parameter)[0])),
                    point.Curvature,
                    point.Parameter);
            }
        }


        var constraints = Constraints;
        TrajectoryPoint[]? trajectoryPoints = null;
        var generateTime = Measurements.MeasureTimeSpan(() =>
        {
            try
            {
                Trajectory = TrajectoryGenerator.GenerateTrajectory(poses.ToArray(), constraints, out trajectoryPoints);
            }
            catch (Exception e)
            {
                GenerateError = e;
            }
        });

        if (GenerateError != null)
        {
            _app.ToastError(GenerateError.Message);

            return false;
        }

        Assert.NotNull(ref trajectoryPoints);

        foreach (var markerEntity in _editor.MarkerPoints.OrderBy(x => x.Get<MarkerComponent>().Parameter))
        {
            var component = markerEntity.Get<MarkerComponent>();

            _markers.Add(new Marker(component.Parameter, component.Name));
        }

        MaxProfileVelocity = trajectoryPoints.MaxBy(x => x.Velocity.LengthSqr).Velocity.Length;
        MaxProfileAcceleration = trajectoryPoints.MaxBy(x => x.Acceleration.LengthSqr).Acceleration.Length;
        MaxProfileAngularVelocity = trajectoryPoints.Max(x => x.AngularVelocity);
        MaxProfileAngularAcceleration = trajectoryPoints.MaxBy(x => x.AngularAcceleration.Abs()).AngularAcceleration;
        Points = trajectoryPoints.Length;

        TotalTime = (float)Trajectory!.TimeRange.End;
        TotalLength = (float)Trajectory.Evaluate(Trajectory.TimeRange.End).Displacement;

        _app.ToastInfo($"{trajectoryPoints.Length} pts. Scan: {getPointsTime.TotalMilliseconds:F2}ms. Gen: {generateTime.TotalMilliseconds:F2}ms");

        return true;
    }

    public bool Update(float dt, out Pose2d pose)
    {
        if (_editor.ArcLength == 0)
        {
            pose = default;

           return false;
        }

        if (Trajectory == null)
        {
            if (GenerateError != null)
            {
                pose = default;
                return false;
            }

            if (_editTime.Elapsed.TotalSeconds < EditTimeRefreshThreshold)
            {
                pose = default;
                return false;
            }

            if (!Generate())
            {
                pose = default;
                return false;
            }
        }

        Last = Trajectory!.Evaluate(((double)PlayTime).Clamped(0, Trajectory.TimeRange.End));
        pose = new Pose2d(Last.CurvePose.Pose.Translation, Last.CurvePose.Pose.Rotation / Rotation2d.Exp(Math.PI / 2));

        foreach (var marker in _markers.Where(x => !_markerEvents.Any(e => e.Marker.Parameter.Equals(x.Parameter))).Where(x => Last.CurvePose.Parameter >= x.Parameter))
        {
            _markerEvents.Add(new MarkerEvent(marker, PlayTime));

            break;
        }
        
        PlayTime += dt * Speed;

        if (PlayTime > Trajectory.TimeRange.End)
        {
            PlayTime = 0;
            _markerEvents.Clear();
        }

        return true;
    }

    public void Dispose()
    {
        
    }
}