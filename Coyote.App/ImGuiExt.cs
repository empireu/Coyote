using ImGuiNET;

namespace Coyote.App;

internal static class ImGuiExt
{
    public static bool LabelScan(string[] names, ref string selected, string comboLbl)
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

    public static bool EnumScan<TEnum>(ref TEnum selected, string comboLbl) where TEnum : struct, Enum
    {
        var selectedName = Enum.GetName(selected) ?? throw new Exception("Failed to get enum name");
        var result = LabelScan(Enum.GetNames<TEnum>(), ref selectedName, comboLbl);
        selected = Enum.Parse<TEnum>(selectedName);

        return result;
    }

    public static TEnum EnumScan<TEnum>(TEnum actual, string comboLbl) where TEnum : struct, Enum
    {
        EnumScan(ref actual, comboLbl);

        return actual;
    }

    public static bool Begin(string title, string id)
    {
        return ImGui.Begin($"{title}##{id}");
    }

    public static bool Begin(string title, string id, ref bool pOpen)
    {
        return ImGui.Begin($"{title}##{id}", ref pOpen);
    }
}