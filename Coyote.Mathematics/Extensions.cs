﻿using System.Numerics;
using GameFramework.Utilities;

namespace Coyote.Mathematics;

public static class Extensions
{
    /// <summary>
    ///     Checks whether <see cref="f"/> is approximately equal to <see cref="other"/> using the specified <see cref="threshold"/>.
    /// </summary>
    /// <param name="f">The first value.</param>
    /// <param name="other">The second value.</param>
    /// <param name="threshold">A comparision threshold.</param>
    /// <returns>True, if the values are no more than <see cref="threshold"/> apart. Otherwise, false.</returns>
    public static bool ApproxEqs(this float f, float other, float threshold = 10e-6f)
    {
        return Math.Abs(f - other) < threshold;
    }

    /// <summary>
    ///     Checks whether <see cref="d"/> is approximately equal to <see cref="other"/> using the specified <see cref="threshold"/>.
    /// </summary>
    /// <param name="d">The first value.</param>
    /// <param name="other">The second value.</param>
    /// <param name="threshold">A comparision threshold.</param>
    /// <returns>True, if the values are no more than <see cref="threshold"/> apart. Otherwise, false.</returns>
    public static bool ApproxEqs(this double d, double other, double threshold = 10e-6)
    {
        return Math.Abs(d - other) < threshold;
    }

    
    public static bool IsNan(this double d)
    {
        return double.IsNaN(d);
    }

    public static Vector2d ToReal2(this Coyote.Mathematics.Vector vector)
    {
        Vectors.Validate(vector, 2);

        return new Vector2d(vector[0], vector[1]);
    }

    public static Vector ToRealVector(this Vector2d v)
    {
        return Vector.Create(v.X, v.Y);
    }

    public static Vector ToRealVector(this Vector2 v)
    {
        return Vector.Create(v.X, v.Y);
    }

    public static Vector2 ToVector2(this Vector vector)
    {
        Vectors.Validate(vector, 2);

        return new Vector2((float)vector[0], (float)vector[1]);
    }

    public static Vector ToRealVector(this double d)
    {
        return Vector.Create(d);
    }

    public static double Squared(this double d) => d * d;
    public static double Abs(this double d) => Math.Abs(d);
    public static double MinWith(this double d, double b) => Math.Min(d, b);
    public static double MaxWith(this double d, double b) => Math.Max(d, b);
    public static double Clamped(this double d, double min, double max) => Math.Clamp(d, min, max);
    public static double Mapped(this double d, double srcMin, double srcMax, double dstMin, double dstMax) => MathUtilities.MapRange(d, srcMin, srcMax, dstMin, dstMax);
    public static double Pow(this double d, double power) => Math.Pow(d, power);

    public static void HashScan(this IEnumerable<CurvePose> poses, FnvStream stream)
    {
        foreach (var curvePose in poses)
        {
            stream.Add(curvePose.Pose);
            stream.Add(curvePose.Curvature);
        }
    }

    public static void HashScan(this IEnumerable<TrajectoryPoint> poses, FnvStream stream)
    {
        foreach (var tp in poses)
        {
            tp.HashScan(stream);
        }
    }

    public static void HashScan(this IEnumerable<QuinticSplineSegment> segments, FnvStream stream)
    {
        foreach (var seg in segments)
        {
            stream.Add(seg.P0);
            stream.Add(seg.V0);
            stream.Add(seg.A0);
            stream.Add(seg.A1);
            stream.Add(seg.V1);
            stream.Add(seg.P1);
        }
    }

    public static void HashScan(this BaseTrajectoryConstraints constraints, FnvStream stream)
    {
        stream.Add(constraints.LinearVelocity);
        stream.Add(constraints.LinearAcceleration);
        stream.Add(constraints.AngularVelocity);
        stream.Add(constraints.AngularAcceleration);
        stream.Add(constraints.CentripetalAcceleration);
    }
}