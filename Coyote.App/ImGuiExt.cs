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
}