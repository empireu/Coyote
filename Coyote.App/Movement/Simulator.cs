using System.Diagnostics;
using Coyote.Mathematics;

namespace Coyote.App.Movement;

internal class Simulator
{
    private const double EditTimeRefreshThreshold = 0.5;

    private readonly App _app;
    private readonly PathEditor _editor;

    private readonly Stopwatch _editTime = Stopwatch.StartNew();

    public Trajectory? Trajectory { get; private set; }
    
    public Simulator(App app, PathEditor editor)
    {
        _app = app;
        _editor = editor;

        _editor.OnTranslationChanged += OnTranslationChanged;
    }

    private void OnTranslationChanged()
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

            var points = new List<CurvePose>();

            Splines.GetPoints(points, _editor.TranslationSpline, Real<Percentage>.Zero, Real<Percentage>.One,
                new Twist(0.001, 0.001, Math.PI / 16), 32000);

            Trajectory = TrajectoryGenerator.Generate(points.ToArray(), new BaseTrajectoryConstraints(
                new Real<Velocity>(2.5),
                new Real<Acceleration>(-1.7),
                new Real<Acceleration>(1.7),
                new Real<CentripetalAcceleration>(Math.PI / 4f)));

            TotalTime = (float)Trajectory.TimeRange.End;
            TotalLength = (float)Trajectory.Evaluate((Real<Time>)Trajectory.TimeRange.End).Displacement;
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