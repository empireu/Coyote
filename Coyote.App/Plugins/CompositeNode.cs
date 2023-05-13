using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

internal abstract class CompositeBase
{
    [StructuredMember] public string Name { get; set; } = "compositeElement";
    [StructuredMember] public string Label { get; set; } = "";

    [JsonIgnore]
    [YamlIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Name : Label;

    public abstract bool SubmitInspector();
}

internal sealed class CompositeFlag : CompositeBase
{
    public bool State { get; set; }

    public override bool SubmitInspector()
    {
        var v = State;
        var flag = ImGui.Checkbox(DisplayLabel, ref v);
        State = v;
        return flag;
    }
}

internal sealed class CompositeEnum : CompositeBase
{
    [StructuredMember] public string[] Options { get; set; } = { "None" };

    public string Selected { get; set; } = "";

    public override bool SubmitInspector()
    {
        var selected = Selected;
        var flag = ImGuiExt.StringComboBox(Options, ref selected, DisplayLabel);
        Selected = selected;
        return flag;
    }
}

internal sealed class CompositeRealSlider : CompositeBase
{
    [StructuredMember] public float Min { get; set; } = 0;
    [StructuredMember] public float Max { get; set; } = 1;

    public float Value { get; set; } = 0;

    public override bool SubmitInspector()
    {
        var value = Value;
        var flag = ImGui.SliderFloat(DisplayLabel, ref value, Min, Max);
        Value = value;
        return flag;
    }
}

internal sealed class CompositeIntegerSlider : CompositeBase
{
    [StructuredMember] public int Min { get; set; } = 0;
    [StructuredMember] public int Max { get; set; } = 10;

    public int Value { get; set; } = 0;

    public override bool SubmitInspector()
    {
        var value = Value;
        var flag = ImGui.SliderInt(DisplayLabel, ref value, Min, Max);
        Value = value;
        return flag;
    }
}

internal sealed class CompositeTextInputField : CompositeBase
{
    [StructuredMember] public uint MaxLength { get; set; } = 512;

    public string Value { get; set; } = string.Empty;

    public override bool SubmitInspector()
    {
        var value = Value;
        var flag = ImGui.InputText(DisplayLabel, ref value, MaxLength);
        Value = value;
        return flag;
    }
}

internal sealed class CompositeRealInputField : CompositeBase
{
    [StructuredMember] public float Step { get; set; } = 0.01f;

    public float Value { get; set; } = 0;
    public override bool SubmitInspector()
    {
        var value = Value;
        var flag = ImGui.InputFloat(DisplayLabel, ref value, Step);
        Value = value;
        return flag;
    }
}

internal sealed class CompositeIntegerInputField : CompositeBase
{
    [StructuredMember] public int Step { get; set; } = 1;

    public int Value { get; set; } = 0;

    public override bool SubmitInspector()
    {
        var value = Value;
        var flag = ImGui.InputInt(DisplayLabel, ref value, Step);
        Value = value;
        return flag;
    }
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

    public IEnumerable<CompositeBase> PropertyScan() => typeof(CompositeState)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(x => x.PropertyType.IsAssignableTo(typeof(IEnumerable)))
        .SelectMany(property => Assert.Is<IEnumerable>(property.GetValue(this)).GetEnumerator().StreamMatching<CompositeBase>());

    [StructuredMember] public bool IsDriveBehavior { get; set; }
    [StructuredMember] public bool IsNonParallel { get; set; }

    public CompositeState CreateInstance()
    {
        // Better implementation would use reflection:

        return JsonSerializer.Deserialize<CompositeState>(JsonSerializer.Serialize(this)) 
               ?? throw new Exception("Failed to clone state");
    }
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

        new HashSet<string>().Also(hs =>
        {
            _structure.PropertyScan().ForEach(c =>
            {
                if (!hs.Add(c.Name))
                {
                    throw new Exception($"Duplicate name \"{c.Name}\"");
                }

                if (string.IsNullOrEmpty(c.Name))
                {
                    throw new Exception($"Invalid name \"{c.Name}\"");
                }

                if (c.Name.Contains(' '))
                {
                    throw new Exception($"Invalid name \"{c.Name}\": cannot bind to remote member.");
                }
            });
        });
       

        if (structure.IsDriveBehavior)
        {
            AddFlags(NodeFlag.DriveBehaviorFlag);
        }

        if (structure.IsNonParallel)
        {
            AddFlags(NodeFlag.NonParallelFlag);
        }
    }

    public override void AttachComponents(Entity entity)
    {
        entity.Add(new CompositeNodeComponent { State = _structure.CreateInstance() });
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

        foreach (var compositeBase in state.PropertyScan())
        {
            ImGui.PushID(id++);
            changed |= compositeBase.SubmitInspector();
            ImGui.PopID();
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