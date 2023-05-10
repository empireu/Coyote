using Veldrid;

namespace Coyote.App.Movement;

[ConfigAccessor]
public static class MotionEditorConfig
{
    public static KeyBind PolarMove = new("Polar Move", Key.AltLeft);
    public static KeyBind AxisMove = new("Axis Move", Key.ShiftLeft);
    public static readonly FloatField PositionKnobSize = new("Position Knob Size", 0.05f);
    public static readonly FloatField RotationKnobSize = new("Rotation Knob Size", 0.035f);
    public static readonly FloatField MarkerSize = new("Marker Size", 0.1f);
    public static readonly FloatField MarkerYOffset = new("Marker Y", 0.07f);
    public static readonly FloatField IndicatorSize = new("Indicator Size", 0.025f);
    public static readonly FloatField PathThickness = new("Path Draw Thickness", 0.015f);
    public static readonly FloatField PathXIncr = new("Path X Incr", 0.1f);
    public static readonly FloatField PathYIncr = new("Path Y Incr", 0.1f);
    public static readonly FloatField PathRotIncr = new("Path Rot Incr", MathF.PI / 32);

}