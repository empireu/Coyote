﻿using System.Text.Json.Serialization;
using Coyote.Mathematics;

namespace Coyote.App.Movement;

internal struct MotionConstraints
{
    public static readonly MotionConstraints Default = new(1.5f, 1f);

    [JsonInclude] 
    public Real<Velocity> MaxVelocity { get; set; }

    [JsonInclude] 
    public Real<Acceleration> MaxAcceleration { get; set; }

    [JsonConstructor]
    public MotionConstraints(Real<Velocity> maxVelocity, Real<Acceleration> maxAcceleration)
    {
        MaxVelocity = maxVelocity;
        MaxAcceleration = maxAcceleration;
    }

    public MotionConstraints(double maxVelocity, double maxAcceleration)
    {
        MaxVelocity = maxVelocity.ToReal<Velocity>();
        MaxAcceleration = maxAcceleration.ToReal<Acceleration>();
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
    public static bool Evaluate(MotionConstraints constraints, double distance, double time, out MotionState state)
    {
        var maxVelocity = constraints.MaxVelocity.Value;
        var maxAcceleration = constraints.MaxAcceleration.Value;

        var accelerationDuration = maxVelocity / maxAcceleration;

        var midpoint = distance / 2;
        var accelerationDistance = 0.5 * maxAcceleration * accelerationDuration * accelerationDuration;

        if (accelerationDistance > midpoint)
        {
            accelerationDuration = Math.Sqrt(midpoint / (0.5 * maxAcceleration));
        }

        accelerationDistance = 0.5 * maxAcceleration * accelerationDuration * accelerationDuration;

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
            state = new MotionState((0.5 * maxAcceleration * time * time).ToReal<Displacement>(), (maxAcceleration * time).ToReal<Velocity>(), maxAcceleration.ToReal<Acceleration>());
            return true;
        }

        if (time < deAccelerationTime)
        {
            accelerationDistance = 0.5 * maxAcceleration * accelerationDuration * accelerationDuration;
            var cruiseCurrentDt = time - accelerationDuration;

            state = new MotionState((accelerationDistance + maxVelocity * cruiseCurrentDt).ToReal<Displacement>(), maxVelocity.ToReal<Velocity>(), Real<Acceleration>.Zero);
            return true;
        }

        accelerationDistance = 0.5 * maxAcceleration * accelerationDuration * accelerationDuration;
        cruiseDistance = maxVelocity * cruiseDuration;
        deAccelerationTime = time - deAccelerationTime;

        state = new MotionState(
            (accelerationDistance + cruiseDistance + maxVelocity * deAccelerationTime - 0.5 * maxAcceleration * deAccelerationTime * deAccelerationTime).ToReal<Displacement>(),
            (maxVelocity - deAccelerationTime * maxAcceleration).ToReal<Velocity>(), 
            maxAcceleration.ToReal<Acceleration>());

        return true;
    }
}