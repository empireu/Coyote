using System.Diagnostics;
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

    public float Dx = 0.001f;
    public float Dy = 0.001f;
    public float DAngleTranslation = MathF.PI / 64;
    public float DParameterTranslation = 0.00025f;
    public float DAngleRotation = MathF.PI / 64;
    public float DParameterRotation = 0.00025f;

    public float MaxLinearVelocity = 1.5f;
    public float MaxLinearAcceleration = 1f;
    public float MaxCentripetalAcceleration = 0.5f;
    public float MaxAngularVelocity = 180f;
    public float MaxAngularAcceleration = 140f;

    public readonly struct Marker
    {
        public Real<Percentage> Parameter { get; }
        public string Label { get; }

        public Marker(Real<Percentage> parameter, string label)
        {
            Parameter = parameter;
            Label = label;
        }
    }

    public readonly struct MarkerEvent
    {
        public MarkerEvent(Marker marker, Real<Time> hitTime)
        {
            Marker = marker;
            HitTime = hitTime;
        }

        public Marker Marker { get; }
        public Real<Time> HitTime { get; }
    }

    private readonly List<MarkerEvent> _markerEvents = new();
    public IEnumerable<MarkerEvent> MarkerEvents => _markerEvents;

    private readonly List<Marker> _markers = new();

    public bool Update(float dt, out Pose pose)
    {
        if (_editor.ArcLength == 0)
        {
           pose = Pose.Zero;

           return false;
        }

        if (Trajectory == null)
        {
            if (_editTime.Elapsed.TotalSeconds < EditTimeRefreshThreshold)
            {
                pose = Pose.Zero;

                return false;
            }

            _markerEvents.Clear();
            _markers.Clear();

            PlayTime = 0;

            var poses = new List<CurvePose>();

            var getPointsTime = Measurements.MeasureTimeSpan(() =>
            {
                Splines.GetPoints(poses,
                    _editor.TranslationSpline,
                    Real<Percentage>.Zero,
                    Real<Percentage>.One,
                    new Real<Percentage>(DParameterTranslation),
                    new Twist(Dx, Dy, DAngleTranslation),
                    int.MaxValue,
                    _editor.RotationSpline.IsEmpty
                        ? null
                        : (t0, t1) =>
                        {
                            if ((t0 - t1).Abs() > DParameterRotation)
                            {
                                return true;
                            }

                            var r0 = _editor.RotationSpline.Evaluate(t0);
                            var r1 = _editor.RotationSpline.Evaluate(t1);

                            return Math.Abs(r0[0] - r1[0]) > DAngleRotation;
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
                        new Pose(point.Pose.Translation, _editor.RotationSpline.Evaluate(point.Parameter)[0]),
                        point.Curvature, 
                        point.Parameter);
                }
            }

            TrajectoryPoint[]? trajectoryPoints = null;
            var generateTime = Measurements.MeasureTimeSpan(() =>
            {
                Trajectory = TrajectoryGenerator.GenerateTrajectory(poses.ToArray(), new BaseTrajectoryConstraints(
                    new Real<Velocity>(MaxLinearVelocity),
                    new Real<Acceleration>(MaxLinearAcceleration),
                    new Real<AngularVelocity>(Angles.ToRadians(MaxAngularVelocity)),
                    new Real<AngularAcceleration>(Angles.ToRadians(MaxAngularAcceleration)),
                    new Real<CentripetalAcceleration>(MaxCentripetalAcceleration)), out trajectoryPoints);
            });

            foreach (var markerEntity in _editor.MarkerPoints.OrderBy(x => x.Get<MarkerComponent>().Parameter))
            {
                var component = markerEntity.Get<MarkerComponent>();
              
                _markers.Add(new Marker(component.Parameter, component.Name));
            }


            Assert.NotNull(ref trajectoryPoints);

            MaxProfileVelocity = trajectoryPoints.MaxBy(x => x.Velocity.LengthSquared()).Velocity.Length();
            MaxProfileAcceleration = trajectoryPoints.MaxBy(x => x.Acceleration.LengthSquared()).Acceleration.Length();
            MaxProfileAngularVelocity = trajectoryPoints.Max(x => x.AngularVelocity);
            MaxProfileAngularAcceleration = trajectoryPoints.MaxBy(x => x.AngularAcceleration.Abs()).AngularAcceleration;
            Points = trajectoryPoints.Length;
            
            TotalTime = (float)Trajectory!.TimeRange.End;
            TotalLength = (float)Trajectory.Evaluate((Real<Time>)Trajectory.TimeRange.End).Displacement;

            _app.ToastInfo($"{trajectoryPoints.Length} pts. Scan: {getPointsTime.TotalMilliseconds:F2}ms. Gen: {generateTime.TotalMilliseconds:F2}ms");
        }

        Last = Trajectory.Evaluate(PlayTime.ToReal<Time>().Clamped(0, Trajectory.TimeRange.End));
        pose = Last.CurvePose.Pose;
        pose -= new Rotation(Math.PI / 2); // Graphic points upwards with identity transform

        foreach (var marker in _markers.Where(x => !_markerEvents.Any(e => e.Marker.Parameter.Equals(x.Parameter))).Where(x => Last.CurvePose.Parameter >= x.Parameter))
        {
            _markerEvents.Add(new MarkerEvent(marker, PlayTime.ToReal<Time>()));

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