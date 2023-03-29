using System.Numerics;
using System.Text.Json.Serialization;
using Vortice.Mathematics;

namespace DashViu.Plugins.FieldViewer.World;

internal readonly struct Pose
{
    [JsonInclude]
    public float X { get; }

    [JsonInclude]
    public float Y { get; }

    [JsonInclude]
    public float Rotation { get; }

    [JsonIgnore]
    public Vector2 RotationVector => new Vector2(MathF.Cos(Rotation), MathF.Sin(Rotation));

    [JsonIgnore] 
    public Vector2 Translation => new(X, Y);

    [JsonConstructor]
    public Pose(float x, float y, float rotation = 0f)
    {
        X = x;
        Y = y;
        Rotation = rotation;
    }

    public Pose(Vector2 translation, float rotation = 0f) : this(translation.X, translation.Y, rotation)
    {
        
    }

    public Pose(Vector2 translation, Vector2 tangent) : this(translation, MathF.Atan2(tangent.Y, tangent.X))
    {

    }

    [JsonIgnore]
    public Vector2 Forward => new(MathF.Cos(Rotation), MathF.Sin(Rotation));

    public static Pose operator +(Pose a, Pose b)
    {
        return new Pose(a.Translation + b.Translation, a.Rotation + b.Rotation);
    }

    public static Pose operator -(Pose a, Pose b)
    {
        return new Pose(a.Translation - b.Translation, a.Rotation - b.Rotation);
    }

    public static Pose operator +(Pose a, Vector2 translation)
    {
        return new Pose(a.Translation + translation, a.Rotation);
    }

    public static Pose operator -(Pose a, Vector2 translation)
    {
        return new Pose(a.Translation - translation, a.Rotation);
    }

    public static Pose operator +(Pose a, float rotation)
    {
        return new Pose(a.Translation, a.Rotation + rotation);
    }

    public static Pose operator -(Pose a, float rotation)
    {
        return new Pose(a.Translation, a.Rotation - rotation);
    }

    public static Pose Zero => new(0, 0);

    public override string ToString()
    {
        return $"{{X={Translation.X:F}, Y={Translation.Y:F}, R={MathHelper.ToDegrees(Rotation):F}" + "}}";
    }
}