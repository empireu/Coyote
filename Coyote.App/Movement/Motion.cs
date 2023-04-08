using System.Text.Json.Serialization;
using Coyote.App.Mathematics;

namespace Coyote.App.Movement;

internal struct MotionConstraints
{
    public static readonly MotionConstraints Default = new MotionConstraints(1.5f, 1f);

    [JsonInclude] 
    public float MaxVelocity { get; set; }

    [JsonInclude] 
    public float MaxAcceleration { get; set; }

    public MotionConstraints(float maxVelocity, float maxAcceleration)
    {
        MaxVelocity = maxVelocity;
        MaxAcceleration = maxAcceleration;
    }
}

internal readonly struct MotionState
{
    public Real<Displacement> Distance { get; }
    public Real<Velocity> Velocity { get; }
    public Real<Acceleration> Acceleration { get; }

    public MotionState(Real<Displacement> distance, Real<Velocity> velocity, Real<Acceleration> acceleration)
    {
        Distance = distance;
        Velocity = velocity;
        Acceleration = acceleration;
    }
}

static class TrapezoidalProfile
{
    public static bool Evaluate(MotionConstraints constraints, float distance, float time, out MotionState state)
    {
        var maxVelocity = constraints.MaxVelocity;
        var maxAcceleration = constraints.MaxAcceleration;

        var accelerationDuration = maxVelocity / maxAcceleration;

        var midpoint = distance / 2f;
        var accelerationDistance = 0.5f * maxAcceleration * accelerationDuration * accelerationDuration;

        if (accelerationDistance > midpoint)
        {
            accelerationDuration = MathF.Sqrt(midpoint / (0.5f * maxAcceleration));
        }

        accelerationDistance = 0.5f * maxAcceleration * accelerationDuration * accelerationDuration;

        maxVelocity = maxAcceleration * accelerationDuration;

        var deAccelerationInterval = accelerationDuration;

        var cruiseDistance = distance - 2 * accelerationDistance;
        var cruiseDuration = cruiseDistance / maxVelocity;
        var deAccelerationTime = accelerationDuration + cruiseDuration;

        var entireDuration = accelerationDuration + cruiseDuration + deAccelerationInterval;

        if (time > entireDuration)
        {
            state = default;
            return false;
        }

        if (time < accelerationDuration)
        {
            state = new MotionState((0.5f * maxAcceleration * time * time).ToReal<Displacement>(), (maxAcceleration * time).ToReal<Velocity>(), maxAcceleration.ToReal<Acceleration>());
            return true;
        }

        if (time < deAccelerationTime)
        {
            accelerationDistance = 0.5f * maxAcceleration * accelerationDuration * accelerationDuration;
            var cruiseCurrentDt = time - accelerationDuration;

            state = new MotionState((accelerationDistance + maxVelocity * cruiseCurrentDt).ToReal<Displacement>(), maxVelocity.ToReal<Velocity>(), Real<Acceleration>.Zero);
            return true;
        }

        accelerationDistance = 0.5f * maxAcceleration * accelerationDuration * accelerationDuration;
        cruiseDistance = maxVelocity * cruiseDuration;
        deAccelerationTime = time - deAccelerationTime;

        state = new MotionState(
            (accelerationDistance + cruiseDistance + maxVelocity * deAccelerationTime - 0.5f * maxAcceleration * deAccelerationTime * deAccelerationTime).ToReal<Displacement>(),
            (maxVelocity - deAccelerationTime * maxAcceleration).ToReal<Velocity>(), 
            maxAcceleration.ToReal<Acceleration>());

        return true;
    }
}