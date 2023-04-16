#define WRITE_TO_FILE

using Coyote.Data;
using GameFramework.Utilities;
using System.Text.Json.Serialization;
using static System.Double;

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

public struct TrajectoryPoint
{
    public CurvePose CurvePose;
    public Real<Curvature> RotationCurvature;
    public Real<Displacement> Displacement;
    public Real<AngularDisplacement> AngularDisplacement;
    public Real<Time> Time;

    public Real2<Velocity> Velocity;
    public Real2<Acceleration> Acceleration;
    public Real<AngularVelocity> AngularVelocity;
    public Real<AngularAcceleration> AngularAcceleration;
}

public sealed class BaseTrajectoryConstraints
{
    public BaseTrajectoryConstraints(
        Real<Velocity> linearVelocity,
        Real<Acceleration> linearAcceleration,
        Real<AngularVelocity> angularVelocity,
        Real<AngularAcceleration> angularAcceleration,
        Real<CentripetalAcceleration> centripetalAcceleration)
    {
        if (linearVelocity <= 0)
        {
            throw new ArgumentException("Max translational velocity must be positive", nameof(linearVelocity));
        }

        if (linearAcceleration <= 0)
        {
            throw new ArgumentException("Max translational acceleration must be positive", nameof(linearAcceleration));
        }

        if (angularVelocity <= 0)
        {
            throw new ArgumentException("Max angular velocity must be positive", nameof(angularVelocity));
        }

        if (angularAcceleration <= 0)
        {
            throw new ArgumentException("Max angular acceleration must be positive", nameof(angularAcceleration));
        }

        if (centripetalAcceleration <= 0)
        {
            throw new ArgumentException("Max centripetal acceleration must be positive", nameof(centripetalAcceleration));
        }

        LinearVelocity = linearVelocity;
        LinearAcceleration = linearAcceleration;
        AngularVelocity = angularVelocity;
        AngularAcceleration = angularAcceleration;
        CentripetalAcceleration = centripetalAcceleration;
    }

    public Real<Velocity> LinearVelocity { get; }
    public Real<Acceleration> LinearAcceleration { get; }
    public Real<AngularVelocity> AngularVelocity { get; }
    public Real<AngularAcceleration> AngularAcceleration { get; }
    public Real<CentripetalAcceleration> CentripetalAcceleration { get; }
}

public static class TrajectoryGenerator
{
    private struct Intermediary
    {
        public Real<Displacement> LinearDisplacement;
        public Real<Velocity> LinearVelocity;
        public Real<Curvature> RotationCurvature;
    }

    /// <summary>
    ///     Computes the displacement at each point, relative to the start point.
    /// </summary>
    private static void AssignPathPoints(TrajectoryPoint[] points)
    {
        points[0].Displacement = Real<Displacement>.Zero;
        points[0].AngularDisplacement = Real<AngularDisplacement>.Zero;

        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            current.Displacement = previous.Displacement + (current.CurvePose.Pose.Translation - previous.CurvePose.Pose.Translation).Displacement.Length();
            current.AngularDisplacement = previous.AngularDisplacement + Angles.DeltaAngle(current.CurvePose.Pose.Rotation.Angle, previous.CurvePose.Pose.Rotation.Angle).Angle.Abs();

            if (current.Displacement == previous.Displacement)
            {
                throw new Exception("Path points with zero displacement are not allowed");
            }

            // Numerically evaluate curvature (will be different to path curvature if the rotation directions are not tangent to the path)
            // We need this to compute the rotation constraints for holonomic paths.
            current.RotationCurvature = MathExt.ComputeCurvature(
                current.CurvePose.Pose.Rotation - previous.CurvePose.Pose.Rotation,
                current.Displacement - previous.Displacement);
        }
    }

    /// <summary>
    ///     Computes the time required to move from point A a specified <see cref="displacement"/>.
    /// </summary>
    /// <typeparam name="TDisplacement">The displacement unit to use.</typeparam>
    /// <typeparam name="TVelocity">The velocity unit to use.</typeparam>
    /// <typeparam name="TAcceleration">The acceleration unit to use.</typeparam>
    /// <param name="displacement">The distance between the two points.</param>
    /// <param name="initialVelocity">The velocity at the first point.</param>
    /// <param name="finalVelocity">the velocity at the destination.</param>
    /// <param name="minAcceleration">The de-acceleration.</param>
    /// <param name="maxAcceleration">The acceleration.</param>
    /// <returns>The time needed to perform this movement.</returns>
    private static Real<Time> ComputeMovementTime<TDisplacement, TVelocity, TAcceleration>(
        Real<TDisplacement> displacement,
        Real<TVelocity> initialVelocity,
        Real<TVelocity> finalVelocity,
        Real<TAcceleration> minAcceleration,
        Real<TAcceleration> maxAcceleration)
    {
        Assert.IsTrue(minAcceleration < 0);
        Assert.IsTrue(maxAcceleration > 0);

        if (displacement == Real<TDisplacement>.Zero)
        {
            if (initialVelocity == finalVelocity)
            {
                // It is probably fine, since time 0 is acceptable.

                return Real<Time>.Zero;
            }

            Assert.Fail("Generation failed");
        }

        var acceleration = (finalVelocity.Squared() - initialVelocity.Squared()) / (2 * displacement);

        if (acceleration.Abs() > 0)
        {
            return ((finalVelocity - initialVelocity) / acceleration).Value.ToReal<Time>();
        }

        var sum = initialVelocity + finalVelocity;

        if (sum == Real<TVelocity>.Zero)
        {
            Assert.Fail("Velocity sum zero");
        }

        return (2 * displacement / sum).ToReal<Time>();
    }

    private static void ComputeVelocityAcceleration(TrajectoryPoint[] points)
    {
        points[0].Velocity = Real2<Velocity>.Zero;
        points[0].AngularVelocity = Real<AngularVelocity>.Zero;

        for (var i = 1; i < points.Length; i++)
        {
            var previous = points[i - 1];
            ref var current = ref points[i];

            var dPos = current.CurvePose.Pose.Translation - previous.CurvePose.Pose.Translation;
            var dTheta = current.AngularDisplacement - previous.AngularDisplacement;
            var dt = current.Time - previous.Time;

            current.Velocity = new Real2<Velocity>(dPos / dt);
            current.AngularVelocity = new Real<AngularVelocity>(dTheta / dt);

            current.Acceleration = new Real2<Acceleration>((current.Velocity - previous.Velocity) / dt);
            current.AngularAcceleration = new Real<AngularAcceleration>((current.AngularVelocity - previous.AngularVelocity) / dt);
        }
    }

    /// <summary>
    ///     Computes upper isolated velocities.
    ///     This also includes upper bounds that ensure rotational acceleration constraints can be approximately respected.
    /// </summary>
    private static void ComputeUpperVelocities(TrajectoryPoint[] poses, Intermediary[] profile, BaseTrajectoryConstraints constraints)
    {
        var awMax = constraints.AngularAcceleration;
        var atMax = constraints.LinearAcceleration;

        #region Angular Acceleration Bounds

        // Find the derivation of this work in http://www2.informatik.uni-freiburg.de/~lau/students/Sprunk2008.pdf
        double Bounds(int index1, int index)
        {
            double Sqr(double a)
            {
                return a * a;
            }

            var ci = profile[index].RotationCurvature;
            var ci1 = profile[index1].RotationCurvature;

            var ds = (poses[index].Displacement - poses[index1].Displacement).Abs();

            double thresh = constraints.LinearVelocity;

            void UnexpectedSet()
            {
                throw new Exception($"Unexpected CI {ci} and CI1 {ci1}");
            }

            if (ci > 0 && ci1 >= 0)
            {
                if (ci > ci1)
                {
                    thresh = Math.Sqrt(2 * ds * Sqr((awMax + ci * atMax)) /
                                       ((atMax * (ci + ci1) + 2 * awMax) * (ci - ci1)));
                }
                else if (ci < ci1)
                {
                    var thresh1 = Math.Sqrt((8 * ci * awMax * ds) / Sqr(ci1 + ci));
                    var tmp1 = Math.Sqrt((4 * ci * ds * (ci * atMax + awMax)) / (Sqr(ci1 - ci)));
                    var tmp2 = Math.Sqrt((2 * ds * Sqr(ci * atMax + awMax)) /
                                         ((ci1 - ci) * (2 * awMax + (ci1 + ci) * atMax)));
                    var thresh_tmp1 = Math.Min(tmp1, tmp2);
                    var thresh_tmp2 = Math.Min(Math.Sqrt((2 * awMax * ds) / (ci1)), Math.Sqrt(2 * atMax * ds));

                    var thresh_tmp3 = NegativeInfinity;

                    var tmp = Math.Min(((2 * awMax * ds) / (ci1)),
                        ((2 * ds * Sqr(ci * atMax - awMax)) / ((ci1 - ci) * (2 * awMax - (ci1 + ci) * atMax))));

                    if (tmp > ((-4 * ci * ds * (ci * atMax - awMax)) / ((ci1 - ci) * (ci1 + ci))) &&
                        tmp > 2 * atMax * ds)
                    {
                        thresh_tmp3 = Math.Sqrt(tmp);
                    }

                    thresh = Math.Max(Math.Max(thresh1, thresh_tmp1), Math.Max(thresh_tmp2, thresh_tmp3));

                }
                else if (ci == ci1)
                {
                    thresh = PositiveInfinity;
                }
                else
                {
                    UnexpectedSet();
                }
            }
            else if (ci < 0 && ci1 <= 0)
            {
                if (ci > ci1)
                {
                    var thresh1 = Math.Sqrt((-8 * ci * awMax * ds) / Sqr(ci1 + ci));
                    var tmp1 = Math.Sqrt((-4 * ci * ds * (awMax - ci * atMax)) / ((ci1 + ci) * (ci1 - ci)));
                    var tmp2 = Math.Sqrt((-2 * ds * Sqr(awMax - ci * atMax)) /
                                         ((ci1 - ci) * (2 * awMax - (ci1 + ci) * atMax)));
                    var thresh_tmp1 = Math.Min(tmp1, tmp2);
                    var thresh_tmp2 = Math.Min(Math.Sqrt((-2 * awMax * ds) / (ci1)), Math.Sqrt(2 * atMax * ds));

                    var thresh_tmp3 = NegativeInfinity;


                    var tmp = Math.Min(((-2 * awMax * ds) / (ci1)),
                        ((-2 * ds * Sqr(ci * atMax - awMax)) / ((ci1 - ci) * (2 * awMax + (ci1 + ci) * atMax))));

                    if (tmp > ((-4 * ci * ds * (awMax + ci * atMax)) / ((ci1 - ci) * (ci1 + ci))) &&
                        tmp > 2 * atMax * ds)
                    {
                        thresh_tmp3 = Math.Sqrt(tmp);
                    }

                    thresh = Math.Max(Math.Max(thresh1, thresh_tmp1), Math.Max(thresh_tmp2, thresh_tmp3));


                }
                else if (ci < ci1)
                {
                    thresh = Math.Sqrt((-2 * ds * Sqr(awMax - ci * atMax)) /
                                       ((ci1 - ci) * ((ci + ci1) * atMax - 2 * awMax)));
                }
                else if (ci == ci1)
                {
                    thresh = PositiveInfinity;
                }
                else
                {
                    UnexpectedSet();
                }
            }
            else if (ci < 0 && ci1 > 0)
            {
                var vtwostarpos = Math.Sqrt((2 * ds * awMax) / (ci1));
                var precond = PositiveInfinity;

                if (ci1 + ci < 0)
                {
                    precond = Math.Sqrt((-4 * ci * ds * (ci * atMax - awMax)) / ((ci1 - ci) * (ci1 + ci)));
                }

                var thresh_tmp = Math.Min(precond,
                    Math.Sqrt((-2 * ds * Sqr(ci * atMax - awMax)) / ((ci1 - ci) * ((ci1 + ci) * atMax - 2 * awMax))));
                thresh_tmp = Math.Max(thresh_tmp, Math.Sqrt(2 * ds * atMax));
                thresh = Math.Min(thresh_tmp, vtwostarpos);

            }
            else if (ci > 0 && ci1 < 0)
            {
                var vonestarpos = Math.Sqrt(-(((2 * ds * awMax) / (ci1))));
                var precond = PositiveInfinity;

                if (ci1 + ci > 0)
                {
                    precond = Math.Sqrt((-4 * ci * ds * (awMax + ci * atMax)) / ((ci1 - ci) * (ci1 + ci)));
                }

                var thresh_tmp = Math.Min(precond,
                    Math.Sqrt((-2 * ds * Sqr(awMax + ci * atMax)) / ((ci1 - ci) * (((ci1 + ci) * atMax + 2 * awMax)))));
                thresh_tmp = Math.Max(thresh_tmp, Math.Sqrt(2 * ds * atMax));
                thresh = Math.Min(thresh_tmp, vonestarpos);

            }
            else if (ci == 0 && ci1 == 0)
            {
                thresh = PositiveInfinity;
            }
            else if (ci == 0)
            {
                if (ci1 > 0)
                {
                    var vtwohatpos = Math.Sqrt((2 * ds * awMax) / (ci1));
                                       
                    var thresh_tmp = MathExt.MaxNaN(
                        Math.Sqrt(2 * ds * atMax),
                        Math.Sqrt((-2 * ds * Sqr(awMax)) / (ci1 * (ci1 * atMax - 2 * awMax))));

                    thresh = MathExt.MinNaN(vtwohatpos, thresh_tmp);

                    if(thresh.IsNan())
                    {
                        Assert.Fail();
                    }
                }
                else if (ci1 < 0)
                {
                    var vonehatpos = Math.Sqrt(-((2 * ds * awMax) / (ci1)));
                  
                    var thresh_tmp = MathExt.MaxNaN(
                        Math.Sqrt(2 * ds * atMax), 
                        Math.Sqrt((-2 * ds * Sqr(awMax)) / (ci1 * (ci1 * atMax + 2 * awMax))));
                    
                    thresh = MathExt.MinNaN(vonehatpos, thresh_tmp);

                    if (thresh.IsNan())
                    {
                        Assert.Fail();
                    }
                }
                else
                {
                    UnexpectedSet();
                }
            }
            else
            {
                UnexpectedSet();
            }

            if (IsNaN(thresh))
            {
                Assert.Fail("Threshold is NaN");
            }

            return thresh;
        }

        profile[0].LinearVelocity = Real<Velocity>.Zero;
        for (var i = 1; i < poses.Length; i++)
        {
            profile[i - 1].LinearVelocity = Math.Min(
                profile[i - 1].LinearVelocity, 
                Bounds(i - 1, i)).ToReal<Velocity>();
        }

        profile[^1].LinearVelocity = Real<Velocity>.Zero;
        for (var i = poses.Length - 2; i >= 0; i--)
        {
            profile[i + 1].LinearVelocity = Math.Min(
                profile[i + 1].LinearVelocity,
                Bounds(i + 1, i)).ToReal<Velocity>();
        }

        #endregion

        // Angular Velocity:
        for (var i = 0; i < poses.Length; i++)
        {
            profile[i].LinearVelocity = profile[i].LinearVelocity.MinWith(
                (constraints.AngularVelocity / profile[i].RotationCurvature.Abs()).Value.ToReal<Velocity>());
        }

        // Centripetal Acceleration:
        for (var i = 0; i < poses.Length; i++)
        { 
            profile[i].LinearVelocity = profile[i].LinearVelocity.MinWith(
                Math.Sqrt(constraints.CentripetalAcceleration / Math.Abs(poses[i].CurvePose.Curvature)).ToReal<Velocity>());
        }
    }

    /// <summary>
    ///     Computes a velocity profile for a holonomic robot using the specified kineto-dynamic constraints.
    /// </summary>
    /// <param name="poses">The basic shape of the path.</param>
    /// <param name="constraints">The base constraints to use.</param>
    /// <returns></returns>
    public static TrajectoryPoint[] GenerateProfile(CurvePose[] poses, BaseTrajectoryConstraints constraints)
    {
#if WRITE_TO_FILE
        
        CsvHeader csvProfileTime = "profile_time";
        CsvHeader csvAngle = "angle";
        CsvHeader csvAngularVelocity = "profile_angular_velocity";
        CsvHeader csvAngularAcceleration = "profile_angular_acceleration";
        CsvHeader csvPathCurvature = "path_curvature";
        CsvHeader csvRotationCurvature = "rotation_curvature";
        CsvHeader csvVelocityUpperBounds = "velocity_upper_bounds";
        CsvHeader csvLinearDisplacement = "linear_displacement";

        using var csv = new CsvWriter(File.CreateText("PROFILE.csv"),
            csvProfileTime,
            csvAngle,
            csvAngularVelocity,
            csvAngularAcceleration,
            csvPathCurvature,
            csvRotationCurvature,
            csvVelocityUpperBounds,
            csvLinearDisplacement);
#endif
        // Validation:

        // Path not really possible with less than two points.
        if (poses.Length < 2)
        {
            throw new ArgumentException("Path is too short", nameof(poses));
        }

        var points = new TrajectoryPoint[poses.Length];

        // Copy the poses to our trajectory points:
        for (var i = 0; i < poses.Length; i++)
        {
            points[i].CurvePose = poses[i];
        }

        AssignPathPoints(points);

        var profile = new Intermediary[points.Length];

        // Assign displacements, curvatures and initial upper velocity:
        for (var i = 0; i < points.Length; i++)
        {
            profile[i] = new Intermediary
            {
                LinearDisplacement = points[i].Displacement,
                LinearVelocity = constraints.LinearVelocity,
                RotationCurvature = points[i].RotationCurvature
            };
        }

        // Compute isolated velocities:
        ComputeUpperVelocities(points, profile, constraints);

#if WRITE_TO_FILE
        var upperBounds = profile.Select(x => x.LinearVelocity).ToArray();
#endif
        // Passes over two points and adjusts velocities so that acceleration constrains are respected.
        void CombinedPass(int previousIndex, int currentIndex)
        {
            // Previous point:
            var pi1 = profile[previousIndex];
            
            // Current point:
            ref var pi = ref profile[currentIndex];

            var translationalDisplacement = pi.LinearDisplacement - pi1.LinearDisplacement;
            
            var atMax = constraints.LinearAcceleration;
            var awMax = constraints.AngularAcceleration;

            var ci1 = profile[previousIndex].RotationCurvature;
            var ci = profile[currentIndex].RotationCurvature;
            var vi1 = pi1.LinearVelocity;

            var ds = translationalDisplacement.Abs();

            // Admissible velocities that respect translational acceleration constraint:
            var vtAt = new Range(
                vi1.Squared() > 2 * atMax * ds
                    ? Math.Sqrt(vi1.Squared() - 2 * atMax * ds)
                    : 0d, 
                Math.Sqrt(vi1.Squared() + 2 * atMax * ds));

            #region Analysis

            double V1()
            {
                return (1d / (2d * ci)) * ((ci1 - ci) * vi1 + Math.Sqrt((ci + ci1).Squared() * vi1.Squared() + 8 * ci * ds * awMax));
            }

            double V2()
            {
                return (1d / (2d * ci)) * ((ci1 - ci) * vi1 - Math.Sqrt((ci + ci1).Squared() * vi1.Squared() + 8 * ci * ds * awMax));
            }

            double V1Star()
            {
                return (1d / (2d * ci)) * ((ci1 - ci) * vi1 + Math.Sqrt((ci + ci1).Squared() * vi1.Squared() - 8 * ci * ds * awMax));
            }

            double V2Star()
            {
                return (1d / (2d * ci)) * ((ci1 - ci) * vi1 - Math.Sqrt((ci + ci1).Squared() * vi1.Squared() - 8 * ci * ds * awMax));
            }

            double V1Hat()
            {
                return -(2 * ds * awMax) / (ci1 * vi1) - vi1;
            }

            double V2Hat()
            {
                return (2 * ds * awMax) / (ci1 * vi1) - vi1;
            }

            #endregion

            // Admissible velocities that respect rotational acceleration constraint:
            var vtAwRanges = Array.Empty<Range>();

            if (ci > 0)
            {
                var condition = (ci + ci1).Squared() * vi1.Squared() - 8 * ci * awMax * ds;

                if (condition < 0)
                {
                    vtAwRanges = new[]
                    {
                        new Range(V2(), V1())
                    };
                }
                else
                {
                    Assert.IsTrue(condition >= 0);

                    vtAwRanges = new[]
                    {
                        new Range(V2(), V2Star()),
                        new Range(V1Star(), V1())
                    };
                }
            }
            else if (ci < 0)
            {
                var condition = (ci + ci1).Squared() * vi1.Squared() + 8 * ci * awMax * ds;

                if (condition < 0)
                {
                    vtAwRanges = new[] { new Range(V1Star(), V2Star()) };
                }
                else
                {
                    Assert.IsTrue(condition >= 0);

                    vtAwRanges = new[]
                    {
                        new Range(V1Star(), V1()),
                        new Range(V2(), V2Star())
                    };
                }
            }
            else
            {
                Assert.IsTrue(ci == 0);

                if (ci1 > 0)
                {
                    vtAwRanges = new[] { new Range(V1Hat(), V2Hat()) };
                }
                else if (ci1 < 0)
                {
                    vtAwRanges = new[] { new Range(V2Hat(), V1Hat()) };
                }
                else if (ci1 == 0)
                {
                    vtAwRanges = new[] { Range.R };
                }
            }

            double velocity = 0;

            for (var i = 0; i < vtAwRanges.Length; i++)
            {
                var vtAw = Range.Intersect(Range.R0Plus, vtAwRanges[i]);
                var intersect = Range.Intersect(vtAw, vtAt);

                if (!Range.CheckValidity(intersect))
                {
                    if (i == vtAwRanges.Length - 1)
                    {
                        var (angVel, linVel) = vtAwRanges
                            .SelectMany(range => new[]
                            {
                                range.Min, 
                                range.Max
                            })
                            .Where(av => av >= -10e-4)
                            .SelectMany(av => new[]
                            {
                                (angVel: av, linVel: vtAt.Min), 
                                (angVel: av, linVel: vtAt.Max)
                            })
                            .MinBy(pair => Math.Abs(pair.angVel - pair.linVel));

                        velocity = Math.Max((linVel + angVel) / 2d, 0);

                        //Console.WriteLine($"Constraint approximation {currentIndex}: {Math.Abs(linVel - angVel) * 100000:F4}");
                    }
                    continue;
                }

                velocity = Math.Max(velocity, intersect.Max);
            }

            if (velocity is NaN || IsInfinity(velocity))
            {
                Assert.Fail();
            }

            pi.LinearVelocity = pi.LinearVelocity.MinWith(velocity.ToReal<Velocity>());
        }

        // Forward pass:
        profile[0].LinearVelocity = Real<Velocity>.Zero;
        for (var i = 1; i < profile.Length; i++)
        {
            CombinedPass(i - 1, i);
        }

        // Backward pass:
        profile[^1].LinearVelocity = Real<Velocity>.Zero;
        for (var i = profile.Length - 2; i >= 0; i--)
        {
            CombinedPass(i + 1, i);
        }

        // Compute time:
        for (var i = 1; i < profile.Length; i++)
        {
            var previous = profile[i - 1];
            var current = profile[i];

            points[i].Time = points[i - 1].Time + ComputeMovementTime(
                current.LinearDisplacement - previous.LinearDisplacement,
                previous.LinearVelocity, 
                current.LinearVelocity, 
                -constraints.LinearAcceleration,
                constraints.LinearAcceleration);
        }

        // Compute actual velocities and accelerations:
        ComputeVelocityAcceleration(points);

#if WRITE_TO_FILE
        for (var i = 0; i < points.Length; i++)
        {
            csv.Add(csvProfileTime, points[i].Time);
            csv.Add(csvAngle, points[i].CurvePose.Pose.Rotation.Angle);
            csv.Add(csvAngularVelocity, points[i].AngularVelocity);
            csv.Add(csvAngularAcceleration, points[i].AngularAcceleration);
            csv.Add(csvPathCurvature, points[i].CurvePose.Curvature);
            csv.Add(csvRotationCurvature, profile[i].RotationCurvature);
            csv.Add(csvVelocityUpperBounds, upperBounds[i]);
            csv.Add(csvLinearDisplacement, profile[i].LinearDisplacement);

            csv.Flush();
        }
#endif
        
        return points;
    }

    public static Trajectory GenerateTrajectory(CurvePose[] poses, BaseTrajectoryConstraints constraints, out TrajectoryPoint[] points)
    {
        points = GenerateProfile(poses, constraints);

        return new Trajectory(points);
    }
}

public class Trajectory
{
    /// <summary>
    ///     Represents a segment between two <see cref="TrajectoryPoint"/>s.
    /// </summary>
    private readonly struct TrajectorySegment
    {
        public TrajectoryPoint A { get; }
        public TrajectoryPoint B { get; }

        public TrajectorySegment(TrajectoryPoint a, TrajectoryPoint b)
        {
            A = a;
            B = b;
        }

        /// <summary>
        ///     Evaluates the trajectory state using interpolation.
        /// </summary>
        /// <param name="t">The absolute time. It must be between the times specified in <see cref="A"/> and <see cref="B"/>.</param>
        /// <returns>An interpolated trajectory state.</returns>
        public TrajectoryPoint Evaluate(Real<Time> t)
        {
            if (t < A.Time || t > B.Time)
            {
                throw new ArgumentOutOfRangeException(nameof(t), $"{t} is outside {A.Time} - {B.Time}");
            }

            // Computes 0-1 progress in this segment:
            var progress = t.MappedTo<Percentage>(A.Time, B.Time, 0, 1);

            var pose = Pose.Lerp(A.CurvePose.Pose, B.CurvePose.Pose, progress);

            return new TrajectoryPoint
            {
                CurvePose = new CurvePose(
                    pose,
                    Real<Curvature>.Lerp(A.CurvePose.Curvature, B.CurvePose.Curvature, progress),
                    Real<Percentage>.Lerp(A.CurvePose.Parameter, B.CurvePose.Parameter, progress)),
                RotationCurvature = Real<Curvature>.Lerp(A.RotationCurvature, B.RotationCurvature, progress),
                Displacement = Real<Displacement>.Lerp(A.Displacement, B.Displacement, progress),
                AngularDisplacement = Real<AngularDisplacement>.Lerp(A.AngularDisplacement, B.AngularDisplacement, progress),
                Time = Real<Time>.Lerp(A.Time, B.Time, progress),

                Velocity = Real2<Velocity>.Lerp(A.Velocity, B.Velocity, progress),
                Acceleration = Real2<Acceleration>.Lerp(A.Acceleration, B.Acceleration, progress),
                AngularVelocity = Real<AngularVelocity>.Lerp(A.AngularVelocity, B.AngularVelocity, progress),
                AngularAcceleration = Real<AngularAcceleration>.Lerp(A.AngularAcceleration, B.AngularAcceleration, progress),
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