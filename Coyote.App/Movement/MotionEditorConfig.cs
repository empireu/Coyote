using Veldrid;

namespace Coyote.App.Movement;

[ConfigAccessor]
public static class MotionEditorConfig
{
    public static KeyBind PolarMove = new("Polar Move", Key.AltLeft);
    public static KeyBind AxisMove = new("Axis Move", Key.ShiftLeft);
    public static readonly DataField<float> PositionKnobSize = new("Position Knob Size", 0.05f);
    public static readonly DataField<float> RotationKnobSize = new("Rotation Knob Size", 0.035f);
    public static readonly DataField<float> MarkerSize = new("Marker Size", 0.1f);
    public static readonly DataField<float> MarkerYOffset = new("Marker Y", 0.07f);
    public static readonly DataField<float> IndicatorSize = new("Indicator Size", 0.025f);
    public static readonly DataField<float> PathThickness = new("Path Draw Thickness", 0.015f);
    public static readonly DataField<float> PathXIncr = new("Path X Incr", 0.1f);
    public static readonly DataField<float> PathYIncr = new("Path Y Incr", 0.1f);
    public static readonly DataField<float> PathRotIncr = new("Path Rot Incr", MathF.PI / 32);
    public static readonly DataField<float> TrajectoryRefreshTime = new("Trajectory Refresh Interval", 0.25f);
}