using System.Diagnostics;
using Coyote.Data;
using Coyote.Mathematics;
using GameFramework.Utilities;

namespace Coyote.App.Movement;

internal class Simulator
{
    private const double EditTimeRefreshThreshold = 0.5;

    private readonly App _app;
    private readonly PathEditor _editor;

    private Trajectory? _trajectory;

    private readonly Stopwatch _editTime = Stopwatch.StartNew();

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
        _trajectory = null;
        _editTime.Restart();
    }

    public float PlayTime { get; private set; }
    public TrajectoryPoint Last { get; private set; }

    public bool Update(float dt, out Pose pose)
    {
        if (_editor.ArcLength == 0)
        {
           pose = Pose.Zero;

           return false;
        }

        if (_trajectory == null)
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


            _trajectory = TrajectoryGenerator.Generate(points.ToArray(), new BaseTrajectoryConstraints(
                new Real<Velocity>(2.5),
                new Real<Acceleration>(-1.7),
                new Real<Acceleration>(1.7),
                new Real<CentripetalAcceleration>(Math.PI / 4f)));
        }

        if (PlayTime > _trajectory.TimeRange.End)
        {
            PlayTime = 0;
            pose = Pose.Zero;
            return false;
        }

        Last = _trajectory.Evaluate(PlayTime.ToReal<Time>());
        pose = Last.CurvePose.Pose;
        pose -= new Rotation(Math.PI / 2);

        PlayTime += dt;

        return true;
    }
}