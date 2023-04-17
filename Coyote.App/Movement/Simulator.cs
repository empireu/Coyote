using System.Diagnostics;
using System.Runtime.InteropServices;
using Coyote.Mathematics;
using GameFramework.Utilities;

namespace Coyote.App.Movement;

internal sealed class Simulator
{
    private const double EditTimeRefreshThreshold = 0.5;

    private const double RotationSplineSplitThreshold = 0.1;
    private const double RotationSplineAngleThreshold = 0.01;

    private readonly App _app;
    private readonly PathEditor _editor;

    private readonly Stopwatch _editTime = Stopwatch.StartNew();

    public Trajectory? Trajectory { get; private set; }

    public Simulator(App app, PathEditor editor)
    {
        _app = app;
        _editor = editor;

        _editor.OnTranslationChanged += OnTranslationChanged;
        _editor.OnRotationChanged += OnRotationChanged;
    }

    private void OnTranslationChanged()
    {
        InvalidateTrajectory();
    }

    private void OnRotationChanged()
    {
        InvalidateTrajectory();
    }

    public void InvalidateTrajectory()
    {
        Trajectory = null;
        _editTime.Restart();
    }

    public float PlayTime { get; private set; }
    public TrajectoryPoint Last { get; private set; }
    public float TotalTime { get; private set; }
    public float TotalLength { get; private set; }

    public float Speed = 1;

    public double MaxVelocity { get; private set; }
    public double MaxAcceleration { get; private set; }
    public double MaxAngularVelocity { get; private set; }
    public double MaxAngularAcceleration { get; private set; }

    public float Dx = 0.0015f;
    public float Dy = 0.0015f;
    public float DTheta = MathF.PI / 64;
    public float DParameter = 0.0005f;

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

            PlayTime = 0;

            var poses = new List<CurvePose>();

            var getPointsTime = Measurements.MeasureTimeSpan(() =>
            {
                Splines.GetPoints(poses,
                    _editor.TranslationSpline,
                    Real<Percentage>.Zero,
                    Real<Percentage>.One,
                    new Real<Percentage>(DParameter),
                    new Twist(Dx, Dy, DTheta),
                    int.MaxValue,
                    _editor.RotationSpline.IsEmpty
                        ? null
                        : (t0, t1) =>
                        {
                            if ((t0 - t1).Abs() > RotationSplineSplitThreshold)
                            {
                                return true;
                            }

                            var r0 = _editor.RotationSpline.Evaluate(t0);
                            var r1 = _editor.RotationSpline.Evaluate(t1);

                            return Math.Abs(r0[0] - r1[0]) > RotationSplineAngleThreshold;
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
                    new Real<Velocity>(2.5),
                    new Real<Acceleration>(1.7),
                    new Real<AngularVelocity>(Angles.ToRadians(220)),
                    new Real<AngularAcceleration>(Angles.ToRadians(200)),
                    new Real<CentripetalAcceleration>(1)), out trajectoryPoints);
            });

            Assert.NotNull(ref trajectoryPoints);

            MaxVelocity = trajectoryPoints.MaxBy(x => x.Velocity.LengthSquared()).Velocity.Length();
            MaxAcceleration = trajectoryPoints.MaxBy(x => x.Acceleration.LengthSquared()).Acceleration.Length();
            MaxAngularVelocity = trajectoryPoints.Max(x => x.AngularVelocity);
            MaxAngularAcceleration = trajectoryPoints.MaxBy(x => x.AngularAcceleration.Abs()).AngularAcceleration;

            TotalTime = (float)Trajectory!.TimeRange.End;
            TotalLength = (float)Trajectory.Evaluate((Real<Time>)Trajectory.TimeRange.End).Displacement;

            _app.ToastInfo($"{trajectoryPoints.Length} pts. Scan: {getPointsTime.TotalMilliseconds:F2}ms. Gen: {generateTime.TotalMilliseconds:F2}ms");
        }

        if (PlayTime > Trajectory.TimeRange.End)
        {
            PlayTime = 0;
            pose = Pose.Zero;
            return false;
        }

        Last = Trajectory.Evaluate(PlayTime.ToReal<Time>());
        pose = Last.CurvePose.Pose;
        pose -= new Rotation(Math.PI / 2);

        PlayTime += dt * Speed;

        return true;
    }
}