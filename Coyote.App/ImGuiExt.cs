using System.Numerics;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using Coyote.Mathematics;
using ImGuiNET;

namespace Coyote.App;

internal static class ImGuiExt
{
    /// <summary>
    ///     Wraps <see cref="ImGui.Combo(string, ref int, string[], int)"/> for use with string arrays and string indices.
    /// </summary>
    public static bool StringComboBox(string[] names, ref string selected, string comboLbl)
    {
        var idx = names.Length == 0 ? -1 : Array.IndexOf(names, selected);

        if (idx == -1)
        {
            idx = 0;
        }

        var result = ImGui.Combo(comboLbl, ref idx, names, names.Length);

        if (names.Length != 0)
        {
            idx = Math.Clamp(idx, 0, names.Length);
            selected = names[idx];
        }

        return result;
    }

    /// <summary>
    ///     Wraps <see cref="StringComboBox"/> for use with enums.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="selected"></param>
    /// <param name="comboLbl"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static bool EnumComboBox<TEnum>(ref TEnum selected, string comboLbl) where TEnum : struct, Enum
    {
        var selectedName = Enum.GetName(selected) ?? throw new Exception("Failed to get enum name");
        var result = StringComboBox(Enum.GetNames<TEnum>(), ref selectedName, comboLbl);
        selected = Enum.Parse<TEnum>(selectedName);

        return result;
    }

    public static bool Begin(string title, string id)
    {
        return ImGui.Begin($"{title}##{id}");
    }

    public static bool Begin(string title, string id, ref bool pOpen)
    {
        return ImGui.Begin($"{title}##{id}", ref pOpen);
    }

    public static bool InputDouble(string label, ref double d, double step = 0.001, double stepFast = 0.1, double min = double.MinValue, double max = double.MaxValue, string format = "%.3f")
    {
        var flag = ImGui.InputDouble(label, ref d, step, stepFast, format);

        if (flag)
        {
            d = Math.Clamp(d, min, max);
        }

        return flag;
    }

    public static bool InputDegrees(string label, ref double radians, double step = 0.001, double stepFast = 0.1, double minDeg = double.MinValue, double maxDeg = double.MaxValue, string format = "%.3f")
    {
        var degrees = Angles.ToDegrees(radians);

        var flag = InputDouble(label, ref degrees, step, stepFast, minDeg, maxDeg, format);

        if (flag)
        {
            radians = Angles.ToRadians(degrees.Clamped(minDeg, maxDeg));
        }

        return flag;
    }

    public static bool InputVector2d(string label, ref Vector2d v, double min = double.MinValue, double max = double.MaxValue, string format = "%.3f")
    {
        var vf = new Vector2((float)v.X, (float)v.Y);
        var flag = ImGui.InputFloat2(label, ref vf, format);
        
        if (flag)
        {
            v = new Vector2d(Math.Clamp(vf.X, min, max), Math.Clamp(vf.Y, min, max));
        }

        return flag;
    }

    public static bool InputDouble3(string label, ref double x, ref double y, ref double z, double min = double.MinValue, double max = double.MaxValue, string format = "%.3f")
    {
        var vf = new Vector3((float)x, (float)y, (float)z);
        var flag = ImGui.InputFloat3(label, ref vf, format);
       
        if (flag)
        {
            x = Math.Clamp(vf.X, min, max);
            y = Math.Clamp(vf.Y, min, max);
            z = Math.Clamp(vf.Z, min, max);
        }
    
        return flag;
    }

    public static bool InputTwist2dIncr(string label, ref Twist2dIncr tw, double min = double.MinValue, double max = double.MaxValue, string format = "%.3f", bool deg = true)
    {
        var x = tw.TrIncr.X;
        var y = tw.TrIncr.Y;
        var r = tw.RotIncr;

        if (deg)
        {
            r = Angles.ToDegrees(r);
        }

        var flag = InputDouble3(label, ref x, ref y, ref r, min, max, format);
     
        if (flag)
        {
            if (deg)
            {
                r = Angles.ToRadians(r);
            }

            tw = new Twist2dIncr(x, y, r);
        }

        return flag;
    }
}