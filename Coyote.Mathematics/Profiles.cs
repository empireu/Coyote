using System.Text.Json.Serialization;
using Coyote.Data;
using GameFramework.Utilities;

namespace Coyote.Mathematics;

public readonly struct MotionConstraints
{
    public static readonly MotionConstraints Default = new(1.5f, 1f);

    [JsonInclude]
    public Real<Velocity> MaxVelocity { get; }

    [JsonInclude]
    public Real<Acceleration> MaxAcceleration { get; }

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

public readonly struct MotionState
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

public static class TrapezoidalProfile
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

public struct TrajectoryPoint
{
    public CurvePose CurvePose;
    public Real<Time> Time;
    public Real<Velocity> Velocity;
    public Real<Acceleration> Acceleration;
    public Real<Displacement> Displacement;

    public Real<Radians> AngularDisplacement;
   
    public Real2<Velocity> CartesianVelocity;
    public Real2<Acceleration> CartesianAcceleration;
}

public readonly struct BaseTrajectoryConstraints
{
    public BaseTrajectoryConstraints(
        Real<Velocity> maxTranslationalVelocity, 
        Real<Acceleration> minTranslationalAcceleration, 
        Real<Acceleration> maxTranslationalAcceleration, 
        Real<CentripetalAcceleration> maxCentripetalAcceleration)
    {
        MaxTranslationalVelocity = maxTranslationalVelocity;
        MinTranslationalAcceleration = minTranslationalAcceleration;
        MaxTranslationalAcceleration = maxTranslationalAcceleration;
        MaxCentripetalAcceleration = maxCentripetalAcceleration;
    }

    public Real<Velocity> MaxTranslationalVelocity { get; }
    public Real<Acceleration> MinTranslationalAcceleration { get; }
    public Real<Acceleration> MaxTranslationalAcceleration { get; }
    public Real<CentripetalAcceleration> MaxCentripetalAcceleration { get; }
}

public class TrajectoryGenerator
{
    private static Real<TVelocity> SolveAchievableVelocity<TVelocity, TAcceleration, TDisplacement>(
        Real<TVelocity> previousVelocity,
        Real<TAcceleration> maxA,
        Real<TDisplacement> displacement) 
        where TVelocity : IUnit 
        where TAcceleration : IUnit 
        where TDisplacement : IUnit
    {
        return Math.Sqrt(previousVelocity.Squared() + 2 * maxA * displacement).ToReal<TVelocity>();
    }

    /// <summary>
    ///     Computes the displacement at each point, relative to the start point.
    /// </summary>
    private static void ComputeDisplacements(TrajectoryPoint[] points)
    {
        points[0].Displacement = Real<Displacement>.Zero;
        points[0].AngularDisplacement = Real<Radians>.Zero;

        var totalDisplacement = Real<Displacement>.Zero;
        var totalAngularDisplacement = Rotation.Zero;

        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            totalDisplacement += (current.CurvePose.Pose.Translation - previous.CurvePose.Pose.Translation).Displacement.Length();
            totalAngularDisplacement += new Rotation((current.CurvePose.Pose.Rotation - previous.CurvePose.Pose.Rotation).Angle);

            current.Displacement = totalDisplacement;
            current.AngularDisplacement = totalAngularDisplacement;
        }
    }

    /// <summary>
    ///     Computes a desired velocity at each point, taking into account some special constraints (e.g. angular constraints, user defined constraints, ...)
    /// </summary>
    private static void DesiredTranslationVelocityPass(TrajectoryPoint[] points, BaseTrajectoryConstraints constraints)
    {
        // Apply initial velocity:
        for (var i = 0; i < points.Length; i++)
        {
            points[i].Velocity = constraints.MaxTranslationalVelocity;
        }

        // Apply extra constraints:
        for (var i = 0; i < points.Length; i++)
        {
            ref var current = ref points[i];

            var maxCurvature = Math.Abs(current.CurvePose.Curvature);

            if (i > 0)
            {
                var previous = points[i - 1];
                var displacement = current.Displacement - previous.Displacement;
                maxCurvature = Math.Max(maxCurvature, (current.CurvePose.Pose.Rotation - previous.CurvePose.Pose.Rotation).Angle.Abs() / displacement);
            }

            // Centripetal acceleration constraint:
            current.Velocity = (Real<Velocity>)Math.Min(
                current.Velocity, 
                Math.Sqrt(constraints.MaxCentripetalAcceleration / maxCurvature));
        }
    }

    /// <summary>
    ///     Compute new velocity based on previous point and max acceleration.
    /// </summary>
    private static void ForwardAccelerationPass(TrajectoryPoint[] points, BaseTrajectoryConstraints constraints)
    {
        points[0].Velocity = Real<Velocity>.Zero;
        points[0].Acceleration = Real<Acceleration>.Zero;

        var maxA = constraints.MaxTranslationalAcceleration;

        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            var displacement = current.Displacement - previous.Displacement;

            var achievableVelocity = SolveAchievableVelocity(previous.Velocity, maxA, displacement);

            var oldVelocity = current.Velocity;

            current.Velocity = Math.Min(achievableVelocity, current.Velocity).ToReal<Velocity>();

            // Compute used acceleration:
            current.Acceleration = current.Velocity.Equals(oldVelocity)
                ? Real<Acceleration>.Zero   // No acceleration
                : maxA;                     // Max acceleration
        }
    }

    /// <summary>
    ///     Compute new velocity based on next point and minimum acceleration.
    /// </summary>
    private static void BackwardAccelerationPass(TrajectoryPoint[] points, BaseTrajectoryConstraints constraints)
    {
        // They should be these anyways, but just to be sure, we set them here:
        points[^1].Velocity = Real<Velocity>.Zero;
        points[^1].Acceleration = Real<Acceleration>.Zero;

        var minA = constraints.MinTranslationalAcceleration;

        for (var i = points.Length - 2; i >= 0; i--)
        {
            var previous = points[i + 1];
            ref var current = ref points[i];

            var displacement = current.Displacement - previous.Displacement;

            var achievableVelocity = SolveAchievableVelocity(previous.Velocity, minA, displacement);

            var oldVelocity = current.Velocity;

            current.Velocity = Math.Min(achievableVelocity, current.Velocity).ToReal<Velocity>();

            // Compute used acceleration:
            current.Acceleration = current.Velocity.Equals(oldVelocity)
                ? current.Acceleration
                : minA;
        }
    }

    private static Real<Time> ComputeTranslationTime(TrajectoryPoint previous, TrajectoryPoint current)
    {
        var displacement = current.Displacement - previous.Displacement;

        Real<Time> translationTime;

        if (current.Acceleration > 0)
        {
            translationTime = (Real<Time>)(-previous.Velocity / current.Acceleration + Math.Sqrt(previous.Velocity.Squared() / current.Acceleration.Squared() + 2 * displacement / current.Acceleration));
        }
        else if (current.Acceleration == Real<Acceleration>.Zero)
        {
            translationTime = new Real<Time>(displacement / previous.Velocity);
        }
        else if (current.Acceleration < 0 && previous.Velocity >= Math.Sqrt(-2 * displacement * current.Acceleration))
        {
            translationTime = (Real<Time>)(-previous.Velocity / current.Acceleration - Math.Sqrt(previous.Velocity.Squared() / current.Acceleration.Squared() + 2 * displacement / current.Acceleration));
        }
        else
        {
            Assert.Fail("No translation time solution found");
            translationTime = Real<Time>.Zero;
        }

        Assert.IsTrue(!double.IsInfinity(translationTime) && !double.IsNaN(translationTime));

        return translationTime;
    }

    /// <summary>
    ///     Computes time at every point.
    /// </summary>
    private static void ComputeTime(TrajectoryPoint[] points)
    {
        points[0].Time = Real<Time>.Zero;

        var totalTime = Real<Time>.Zero;

        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            totalTime += ComputeTranslationTime(previous, current);
            current.Time = totalTime;
        }
    }

    private static void ComputeVelocityAcceleration(TrajectoryPoint[] points)
    {
        points[0].CartesianVelocity = Real2<Velocity>.Zero;
        points[0].CartesianAcceleration = Real2<Acceleration>.Zero;

        // Cartesian velocities:
        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            var dPos = current.CurvePose.Pose.Translation - previous.CurvePose.Pose.Translation;
            var dt = current.Time - previous.Time;

            current.CartesianVelocity = new Real2<Velocity>(dPos / dt);
        }

        // Cartesian accelerations:
        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            var dVel = current.CartesianVelocity - previous.CartesianVelocity;
            var dt = current.Time - previous.Time;

            current.CartesianAcceleration = new Real2<Acceleration>(dVel / dt);
        }
    }

    public static TrajectoryPoint[] GeneratePoints(CurvePose[] poses, BaseTrajectoryConstraints constraints)
    {
        var points = new TrajectoryPoint[poses.Length];

        // Copy the poses to our trajectory points:
        for (var i = 0; i < poses.Length; i++)
        {
            points[i].CurvePose = poses[i];
        }

        // Compute displacement at each point:
        ComputeDisplacements(points);

        // Get basic velocities we want to achieve:
        DesiredTranslationVelocityPass(points, constraints);

        // Apply acceleration constraints:
        ForwardAccelerationPass(points, constraints);

        // Apply de-acceleration constraints:
        BackwardAccelerationPass(points, constraints);

        // Compute times:
        ComputeTime(points);

        // Compute velocities and accelerations:
        ComputeVelocityAcceleration(points);

        return points;
    }

    public static Trajectory Generate(CurvePose[] poses, BaseTrajectoryConstraints constraints, out TrajectoryPoint[] points)
    {
        points = GeneratePoints(poses, constraints);

        return new Trajectory(points);
    }
}

public class Trajectory
{
    private readonly struct TrajectorySegment
    {
        public TrajectoryPoint A { get; }
        public TrajectoryPoint B { get; }

        public TrajectorySegment(TrajectoryPoint a, TrajectoryPoint b)
        {
            A = a;
            B = b;
        }

        public TrajectoryPoint Evaluate(Real<Time> t)
        {
            if (t < A.Time || t > B.Time)
            {
                Assert.Fail();
            }

            var progress = t.MappedTo<Percentage>(A.Time, B.Time, 0, 1);
            var time = Real<Time>.Lerp(A.Time, B.Time, progress);
            var pose = A.CurvePose.Pose.Interpolate(B.CurvePose.Pose, progress);
            var velocity = Real<Velocity>.Lerp(A.Velocity, B.Velocity, progress);
            var acceleration = Real<Acceleration>.Lerp(A.Acceleration, B.Acceleration, progress);
            var curvature = Real<Curvature>.Lerp(A.CurvePose.Curvature, B.CurvePose.Curvature, progress);
            var displacement = Real<Displacement>.Lerp(A.Displacement, B.Displacement, progress);
            var parameter = Real<Percentage>.Lerp(A.CurvePose.Parameter, B.CurvePose.Parameter, progress);

            var cartesianVelocity = Real2<Velocity>.Lerp(A.CartesianVelocity, B.CartesianVelocity, progress);
            var cartesianAcceleration = Real2<Acceleration>.Lerp(A.CartesianAcceleration, B.CartesianAcceleration, progress);

            var angularDisplacement = Real<Radians>.Lerp(A.AngularDisplacement, B.AngularDisplacement, progress);

            return new TrajectoryPoint
            {
                CurvePose = new CurvePose(pose, curvature, parameter),
                Time = time,
                Velocity = velocity,
                Acceleration = acceleration,
                Displacement = displacement,
                
                CartesianVelocity = cartesianVelocity,
                CartesianAcceleration = cartesianAcceleration,
                AngularDisplacement = angularDisplacement
            };
        }
    }

    private readonly SegmentTree<TrajectorySegment> _segments;

    public SegmentRange TimeRange => _segments.Range;

    public Trajectory(TrajectoryPoint[] points)
    {
        if (points.Length < 2)
        {
            throw new ArgumentException("Cannot build trajectory with less than 2 points!");
        }

        var builder = new SegmentTreeBuilder<TrajectorySegment>();

        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            var current = points[i];

            var segment = new TrajectorySegment(previous, current);

            builder.Insert(segment, new SegmentRange(previous.Time, current.Time));
        }

        _segments = builder.Build();
    }

    public TrajectoryPoint Evaluate(Real<Time> time)
    {
        time = time.Clamped(_segments.Range.Start, _segments.Range.End);

        var segment = _segments.Query(time);

        return segment.Evaluate(time);
    }
}