using System.Numerics;
using System.Text.Json.Serialization;

namespace Coyote.App;

public struct JsonVector2
{
    [JsonInclude]
    public float X { get; set; }

    [JsonInclude]
    public float Y { get; set; }

    public static implicit operator Vector2(JsonVector2 v) => new(v.X, v.Y);
    public static implicit operator JsonVector2(Vector2 v) => new() {X = v.X, Y = v.Y };
}