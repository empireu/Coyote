using Coyote.App.Mathematics;
using GameFramework.Utilities;

namespace Coyote.App.Movement;

internal class Simulator
{
    private readonly App _app;
    private readonly PathEditor _editor;

    private ArcParameterizedQuinticSpline? _parameterizedQuinticSpline;
    private float _playTime;

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
        _parameterizedQuinticSpline = null;
    }

    public bool Update(float dt, out Pose pose)
    {
        if (_editor.ArcLength == 0)
        {
           pose = Pose.Zero;

           return false;
        }

        _parameterizedQuinticSpline ??= new ArcParameterizedQuinticSpline(_editor.TranslationSpline.Segments);

        if (!TrapezoidalProfile.Evaluate(_app.Project.Constraints, _parameterizedQuinticSpline.ArcLength, _playTime, out var motionState))
        {
            _playTime = 0;
            
            pose = Pose.Zero;

            return false;
        }

        var parameter = _parameterizedQuinticSpline.EvaluateParameter(motionState.Distance);

        var translation = _parameterizedQuinticSpline.EvaluateUnderlying(parameter);

        pose = _editor.RotationSpline.IsEmpty
            ? new Pose(translation, _parameterizedQuinticSpline.EvaluateUnderlyingVelocity(parameter)) // Spline Tangent Heading
            : new Pose(translation, (float)_editor.RotationSpline.Evaluate(parameter)); // Spline Spline Heading

        pose -= MathF.PI / 2f;

        _playTime += dt;

        return true;
    }
}