using System.Text.Json.Serialization;

namespace Coyote.Mathematics;

/// <summary>
///     Represents a <see cref="Transformation"/> of a <see cref="Pose"/>.
/// </summary>
public readonly struct Transformation
{
    /// <summary>
    ///     Gets an identity <see cref="Transformation"/>. Applying this transformation will leave the <see cref="Pose"/> unaffected.
    /// </summary>
    public static readonly Transformation Identity = new(Translation.Zero, Rotation.Zero);

    [JsonInclude]
    public Translation Translation { get; }

    [JsonInclude]
    public Rotation Rotation { get; }

    [JsonConstructor]
    public Transformation(Translation translation, Rotation rotation)
    {
        Translation = translation;
        Rotation = rotation;
    }

    /// <summary>
    ///     Computes a transformation using an initial state and a final state.
    /// </summary>
    /// <param name="initial">The initial state, before the transformation.</param>
    /// <param name="final">The final state, after the transformation.</param>
    public Transformation(Pose initial, Pose final)
    {
        Translation = (final.Translation - initial.Translation).Rotated(-initial.Rotation);
        Rotation = final.Rotation - initial.Rotation;
    }
}