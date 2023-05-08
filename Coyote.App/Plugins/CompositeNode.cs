using System.Data.Common;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.App.Nodes;
using GameFramework.Assets;
using GameFramework.Renderer;
using ImGuiNET;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Coyote.App.Plugins;

// P.S. "Composite" means user-defined here. We should find a better name.

internal interface IStructuredElement<in TSelf>
{
    bool MatchesStructure(TSelf other);
}

internal static class StructuredElement
{
    public static bool MatchesMany<TElement>(TElement[] a, TElement[] b) where TElement : IStructuredElement<TElement>
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].MatchesStructure(b[i]))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class CompositeFlag : IStructuredElement<CompositeFlag>
{
    public bool MatchesStructure(CompositeFlag other)
    {
        return Name.Equals(other.Name);
    }

    [JsonInclude]
    public string Name { get; set; } = "Flag";

    [JsonInclude]
    public bool State { get; set; }
}

internal sealed class CompositeEnum : IStructuredElement<CompositeEnum>
{
    public bool MatchesStructure(CompositeEnum other)
    {
        return Name.Equals(other.Name) && Options.SequenceEqual(other.Options);
    }

    [JsonInclude]
    public string Name { get; set; } = "Option";
    
    [JsonInclude]
    public string[] Options { get; set; } = new [] { "None" };

    [JsonInclude]
    public string Selected { get; set; } = "";
}

internal sealed class CompositeState : IStructuredElement<CompositeState>
{
    public bool MatchesStructure(CompositeState other)
    {
        return StructuredElement.MatchesMany(Flags, other.Flags) && StructuredElement.MatchesMany(Enums, other.Enums);
    }

    [JsonInclude]
    public CompositeFlag[] Flags { get; set; } = Array.Empty<CompositeFlag>();

    [JsonInclude]
    public CompositeEnum[] Enums { get; set; } = Array.Empty<CompositeEnum>();
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

        if (!savedState.MatchesStructure(_structure))
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

        return changed;
    }

    private struct YamlData
    {
        public string? TexturePath { get; set; }
        public string NodeName { get; set; }
        public Vector4 Color { get; set; }
        public CompositeState? Structure { get; set; }
    }

    public static CompositeNode Load(App app, string path)
    {
        using var reader = File.OpenText(path);

        var data = Deserializer.Deserialize<YamlData>(reader);

        if (string.IsNullOrEmpty(data.TexturePath))
        {
            data.TexturePath = $"{Path.GetFileNameWithoutExtension(path)}.png";
        }

        if (string.IsNullOrEmpty(data.NodeName))
        {
            throw new Exception("Invalid node name");
        }

        data.Structure ??= new CompositeState(); // Symbolic node (without any special data)

        var texturePath = Path.Combine(
            Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Failed to find directory of {path}"),
            data.TexturePath);

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