using System.Text.Json.Serialization;

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
    public MotionState(float distance, float velocity, float acceleration)
    {
        Distance = distance;
        Velocity = velocity;
        Acceleration = acceleration;
    }

    public float Distance { get; }
    public float Velocity { get; }
    public float Acceleration { get; }
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
            state = new MotionState(0.5f * maxAcceleration * time * time, maxAcceleration * time, maxAcceleration);
            return true;
        }

        if (time < deAccelerationTime)
        {
            accelerationDistance = 0.5f * maxAcceleration * accelerationDuration * accelerationDuration;
            var cruiseCurrentDt = time - accelerationDuration;

            state = new MotionState(accelerationDistance + maxVelocity * cruiseCurrentDt, maxVelocity, 0);
            return true;
        }

        accelerationDistance = 0.5f * maxAcceleration * accelerationDuration * accelerationDuration;
        cruiseDistance = maxVelocity * cruiseDuration;
        deAccelerationTime = time - deAccelerationTime;

        state = new MotionState(accelerationDistance + cruiseDistance + maxVelocity * deAccelerationTime -
                                0.5f * maxAcceleration * deAccelerationTime * deAccelerationTime,
            maxVelocity - deAccelerationTime * maxAcceleration, maxAcceleration);

        return true;
    }
}