using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.App.Nodes;
using GameFramework.Assets;
using GameFramework.Renderer;
using GameFramework.Utilities;
using ImGuiNET;
using YamlDotNet.Serialization;

namespace Coyote.App.Plugins;

// P.S. "Composite" means user-defined here. We should find a better name.

[AttributeUsage(AttributeTargets.Property)]
internal sealed class StructuredMemberAttribute : Attribute { }

internal static class CompositeStructure
{
    public static bool Matches(object? a, object? b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a != null && b == null)
        {
            return false;
        }

        if (a == null && b != null)
        {
            return false;
        }

        Assert.NotNull(ref a);
        Assert.NotNull(ref b);

        if (a.GetType() != b.GetType())
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        return a.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<StructuredMemberAttribute>() != null)
            .Bind()
            .Map(properties =>
            {
                if (properties.Length == 0)
                {
                    return a.Equals(b);
                }

                return properties.All(property =>
                {
                    var aValue = property.GetValue(a);
                    var bValue = property.GetValue(b);

                    if (aValue is not IEnumerable aEnumerable)
                    {
                        return Matches(aValue, bValue);
                    }

                    var aEnumerator = aEnumerable.GetEnumerator();
                    var bEnumerator = Assert.Is<IEnumerable>(bValue).GetEnumerator();

                    while (aEnumerator.MoveNext())
                    {
                        if (!bEnumerator.MoveNext())
                        {
                            return false;
                        }

                        if (!Matches(aEnumerator.Current, bEnumerator.Current))
                        {
                            return false;
                        }
                    }

                    return !bEnumerator.MoveNext();
                });
            });
    }
}

#region Composite Data

internal sealed class CompositeFlag
{
    [StructuredMember] public string Name { get; set; } = "Flag";

    public bool State { get; set; }
}

internal sealed class CompositeEnum
{
    [StructuredMember] public string Name { get; set; } = "Option";
    [StructuredMember] public string[] Options { get; set; } = { "None" };

    public string Selected { get; set; } = "";
}

internal sealed class CompositeRealSlider
{
    [StructuredMember] public string Name { get; set; } = "Real Slider";
    [StructuredMember] public float Min { get; set; } = 0;
    [StructuredMember] public float Max { get; set; } = 1;

    public float Value { get; set; } = 0;
}

internal sealed class CompositeIntegerSlider
{
    [StructuredMember] public string Name { get; set; } = "Integer Slider";
    [StructuredMember] public int Min { get; set; } = 0;
    [StructuredMember] public int Max { get; set; } = 10;

    public int Value { get; set; } = 0;
}

internal sealed class CompositeTextInputField
{
    [StructuredMember] public string Name { get; set; } = "Text Input Field";
    [StructuredMember] public uint MaxLength { get; set; } = 512;

    public string Value { get; set; } = string.Empty;
}

internal sealed class CompositeRealInputField
{
    [StructuredMember] public string Name { get; set; } = "Real Input Field";
    [StructuredMember] public float Step { get; set; } = 0.01f;

    public float Value { get; set; } = 0;
}

internal sealed class CompositeIntegerInputField
{
    [StructuredMember] public string Name { get; set; } = "Integer Input Field";
    [StructuredMember] public int Step { get; set; } = 1;

    public int Value { get; set; } = 0;
}

#endregion

internal sealed class CompositeState
{
    [StructuredMember] public CompositeFlag[] Flags { get; set; } = Array.Empty<CompositeFlag>();
    [StructuredMember] public CompositeEnum[] Enums { get; set; } = Array.Empty<CompositeEnum>();
    [StructuredMember] public CompositeRealSlider[] RealSliders { get; set; } = Array.Empty<CompositeRealSlider>();
    [StructuredMember] public CompositeIntegerSlider[] IntegerSliders { get; set; } = Array.Empty<CompositeIntegerSlider>();
    [StructuredMember] public CompositeTextInputField[] TextInputFields { get; set; } = Array.Empty<CompositeTextInputField>();
    [StructuredMember] public CompositeRealInputField[] RealInputFields { get; set; } = Array.Empty<CompositeRealInputField>();
    [StructuredMember] public CompositeIntegerInputField[] IntegerInputFields { get; set; } = Array.Empty<CompositeIntegerInputField>();
}

internal sealed class CompositeNode : NodeBehavior
{
    private readonly CompositeState _structure;

    private struct CompositeNodeComponent
    {
        public CompositeState State;
    }

    public CompositeNode(TextureSampler icon, Vector4 backgroundColor, string name, CompositeState structure) : base(icon, backgroundColor, name)
    {
        _structure = structure;
    }

    public override void AttachComponents(Entity entity)
    {
        entity.Add(new CompositeNodeComponent { State = _structure });
    }

    public override void InitialLoad(Entity entity, string storedData)
    {
        var savedState = JsonSerializer.Deserialize<CompositeState>(storedData) 
                         ?? throw new Exception($"Failed to load composite node {Name}.");

        if (!CompositeStructure.Matches(savedState, _structure))
        {
            throw new Exception($"Saved node {Name} does not match definition.");
        }

        entity.Get<CompositeNodeComponent>().State = savedState;
    }

    public override string Save(Entity entity)
    {
        return JsonSerializer.Serialize(entity.Get<CompositeNodeComponent>().State);
    }

    public override bool SubmitInspector(Entity entity, INodeEditor editor)
    {
        ref var state = ref entity.Get<CompositeNodeComponent>().State;

        nint id = 1;

        var changed = false;
        
        foreach (var composedFlag in state.Flags)
        {
            var flag = composedFlag.State;
            ImGui.PushID(id++);
            changed |= ImGui.Checkbox(composedFlag.Name, ref flag);
            ImGui.PopID();
            composedFlag.State = flag;
        }

        foreach (var composedEnum in state.Enums)
        {
            var selected = composedEnum.Selected;
            ImGui.PushID(id++);
            changed |= ImGuiExt.StringComboBox(composedEnum.Options, ref selected, composedEnum.Name);
            ImGui.PopID();
            composedEnum.Selected = selected;
        }

        foreach (var compositeDoubleSlider in state.RealSliders)
        {
            var value = compositeDoubleSlider.Value;
            ImGui.PushID(id++);
            changed |= ImGui.SliderFloat(compositeDoubleSlider.Name, ref value, compositeDoubleSlider.Min, compositeDoubleSlider.Max);
            ImGui.PopID();
            compositeDoubleSlider.Value = value;
        }

        foreach (var compositeIntegerSlider in state.IntegerSliders)
        {
            var value = compositeIntegerSlider.Value;
            ImGui.PushID(id++);
            changed |= ImGui.SliderInt(compositeIntegerSlider.Name, ref value, compositeIntegerSlider.Min, compositeIntegerSlider.Max);
            ImGui.PopID();
            compositeIntegerSlider.Value = value;
        }

        foreach (var compositeInputField in state.TextInputFields)
        {
            var value = compositeInputField.Value;
            ImGui.PushID(id++);
            changed |= ImGui.InputText(compositeInputField.Name, ref value, compositeInputField.MaxLength);
            ImGui.PopID();
            compositeInputField.Value = value;
        }

        foreach (var compositeRealInputField in state.RealInputFields)
        {
            var value = compositeRealInputField.Value;
            ImGui.PushID(id++);
            changed |= ImGui.InputFloat(compositeRealInputField.Name, ref value, compositeRealInputField.Step);
            ImGui.PopID();
            compositeRealInputField.Value = value;
        }

        foreach (var compositeIntegerInputField in state.IntegerInputFields)
        {
            var value = compositeIntegerInputField.Value;
            ImGui.PushID(id++);
            changed |= ImGui.InputInt(compositeIntegerInputField.Name, ref value, compositeIntegerInputField.Step);
            ImGui.PopID();
            compositeIntegerInputField.Value = value;
        }

        return changed;
    }

    private struct YamlData
    {
        public string? Texture { get; set; }
        public string NodeName { get; set; }
        public Vector4 Color { get; set; }
        public CompositeState? Structure { get; set; }
    }

    public static CompositeNode Load(App app, string path)
    {
        using var reader = File.OpenText(path);

        var data = Deserializer.Deserialize<YamlData>(reader);

        if (string.IsNullOrEmpty(data.Texture))
        {
            data.Texture = $"{Path.GetFileNameWithoutExtension(path)}.png";
        }

        if (string.IsNullOrEmpty(data.NodeName))
        {
            throw new Exception("Invalid node name");
        }

        data.Structure ??= new CompositeState(); // Symbolic node (without any special data)

        var texturePath = Path.Combine(
            Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Failed to find directory of {path}"),
            data.Texture);

        if (!File.Exists(texturePath))
        {
            throw new Exception($"Failed to find texture for node {texturePath} ({path})");
        }

        return new CompositeNode(
            app.Resources.AssetManager.GetSpriteForTexture(new FileResourceKey(texturePath)).Texture,
            data.Color,
            data.NodeName,
            data.Structure);
    }

    private static readonly Deserializer Deserializer = new();
}