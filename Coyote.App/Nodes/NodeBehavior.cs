using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework.Renderer;
using GameFramework.Utilities;
using ImGuiNET;

namespace Coyote.App.Nodes;

public enum NodeTerminalType
{
    Parent,
    Children
}

public class NodeTerminal
{
    public NodeTerminal(NodeTerminalType type, int id)
    {
        Type = type;
        Id = id;
    }

    public NodeTerminalType Type { get; }

    /// <summary>
    ///     An ID used for serialization. This must be unique (per <see cref="NodeConnectionSet"/>)
    /// </summary>
    public int Id { get; }

    /// <summary>
    ///     Checks whether this terminal can connect to another terminal.
    /// </summary>
    /// <param name="other">The remote terminal.</param>
    /// <param name="actualEntity">The entity that owns this terminal.</param>
    /// <param name="remoteEntity">The entity that owns the remote terminal.</param>
    /// <returns></returns>
    public virtual bool AcceptsConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity)
    {
        return true;
    }

    /// <summary>
    ///     Called when a connection is being made, before <see cref="FinishConnection"/>
    /// </summary>
    /// <param name="other">The remote terminal.</param>
    /// <param name="actualEntity">The entity that owns this terminal.</param>
    /// <param name="remoteEntity">The entity that owns the remote terminal.</param>
    public virtual void PrepareConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity) { }

    /// <summary>
    ///     Called when a connection is finalized, after <see cref="PrepareConnection"/>
    /// </summary>
    /// <param name="other">The remote terminal.</param>
    /// <param name="actualEntity">The entity that owns this terminal.</param>
    /// <param name="remoteEntity">The entity that owns the remote terminal.</param>
    public virtual void FinishConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity) { }
}

public class NodeConnectionSet
{
    private readonly List<NodeTerminal> _childTerminals = new();

    public NodeConnectionSet(NodeTerminal parentTerminal)
    {
        ParentTerminal = parentTerminal;

    }

    public NodeTerminal GetTerminal(int id)
    {
        return _childTerminals.Find(x => x.Id == id) 
               ?? throw new Exception($"Failed to get terminal with ID {id}");
    }

    public NodeTerminal ParentTerminal { get; }

    public IReadOnlyList<NodeTerminal> ChildTerminals => _childTerminals;

    public void AddChildTerminal(NodeTerminal child)
    {
        if (_childTerminals.Contains(child))
        {
            throw new Exception("Duplicate child terminal");
        }

        if (_childTerminals.Any(x => x.Id == child.Id))
        {
            throw new Exception("Duplicate child terminal ID");
        }

        if (child.Type != NodeTerminalType.Children)
        {
            throw new ArgumentException("Invalid terminal type", nameof(child));
        }

        _childTerminals.Add(child);
    }

    public void RemoveChildTerminal(NodeTerminal child)
    {
        if (!_childTerminals.Remove(child))
        {
            throw new InvalidOperationException("This set didn't have the specified child terminal");
        }
    }

    // Maybe get rid of this borderSize

    public Vector2 GetParentPosition(Entity entity, float borderSize)
    {
        return entity.Get<PositionComponent>().Position + entity.Get<ScaleComponent>().Scale with { Y = borderSize / 2f } / 2f;
    }

    public Vector2 GetChildPosition(NodeTerminal terminal, Entity entity, float borderSize)
    {
        var i = _childTerminals.IndexOf(terminal);

        if (i == -1)
        {
            throw new ArgumentException("Invalid child terminal", nameof(terminal));
        }

        var positionComponent = entity.Get<PositionComponent>();
        var scaleComponent = entity.Get<ScaleComponent>();

        var startX = positionComponent.Position.X + scaleComponent.Scale.X * 0.1f;
        var endX = positionComponent.Position.X + scaleComponent.Scale.X * 0.9f;

        var x = MathUtilities.MapRange((i + 0.5f) / ChildTerminals.Count, 0f, 1f, startX, endX);
        
        return new Vector2(x, positionComponent.Position.Y - scaleComponent.Scale.Y - borderSize / 4f);
    }
}

public readonly struct NodeChild
{
    public Entity Entity { get; }
    public NodeTerminal Terminal { get; }

    public NodeChild(Entity entity, NodeTerminal terminal)
    {
        Entity = entity;
        Terminal = terminal;
    }
}

public struct NodeComponent
{
    public NodeBehavior Behavior;

    [StringEditor]
    public string Description;

    public Entity? Parent;
    public Ref<List<NodeChild>> ChildrenRef;

    [StringEditor]
    public string Name;

    [BoolEditor]
    public bool ExecuteOnce;

    public NodeConnectionSet Terminals;
}

public sealed class NodeAnalysis
{
    private readonly List<Message> _messages;

    public static Vector4 MessageColor(MessageType type)
    {
        return type switch
        {
            MessageType.Warning => new Vector4(1f, 1f, 0f, 0.8f),
            MessageType.Error => new Vector4(1f, 0f, 0f, 1f),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public enum MessageType
    {
        Warning,
        Error
    }
   
    public readonly struct Message
    {
        public MessageType Type { get; }
        public string Text { get; }

        public Message(MessageType type, string text)
        {
            Type = type;
            Text = text;
        }
    }

    public NodeAnalysis(List<Message> messages)
    {
        _messages = messages;
    }

    public NodeAnalysis With(Message message)
    {
        _messages.Add(message);
        return this;
    }
    public NodeAnalysis Warn(string text) => With(new Message(MessageType.Warning, text));
    public NodeAnalysis Error(string text) => With(new Message(MessageType.Error, text));
}

public abstract class NodeBehavior
{
    /// <summary>
    ///     Unique name for this <see cref="NodeBehavior"/>. This is also used during serialization.
    /// </summary>
    public string Name { get; }

    protected NodeBehavior(TextureSampler icon, Vector4 backgroundColor, string name)
    {
        Name = name;
        Icon = icon;
        BackgroundColor = backgroundColor;
    }

    public TextureSampler Icon { get; set; }

    public Vector4 BackgroundColor { get; set; }

    public Entity CreateEntity(World world, Vector2 position)
    {
        var entity = world.Create(
            new PositionComponent { Position = position },
            new NodeComponent
            {
                Behavior = this,
                ChildrenRef = new Ref<List<NodeChild>>(new List<NodeChild>()),
                Description = ToString() ?? string.Empty,
                ExecuteOnce = true,
                Name = string.Empty,
                Terminals = new NodeConnectionSet(new NodeTerminal(NodeTerminalType.Parent, 0))
                    .Also(AttachTerminals)
            },
            new ScaleComponent());

        AttachComponents(entity);

        return entity;
    }

    /// <summary>
    ///     Called after an entity was created.
    ///     This is used to attach the child terminals to it.
    /// </summary>
    /// <param name="connections"></param>
    protected virtual void AttachTerminals(NodeConnectionSet connections)
    {

    }

    /// <summary>
    ///     Called after an entity was created.
    ///     This is used to attach extra components to it.
    /// </summary>
    /// <param name="entity"></param>
    public virtual void AttachComponents(Entity entity)
    {
        
    }

    /// <summary>
    ///     Checks if <see cref="actualEntity"/> can have the entity <see cref="remoteEntity"/> as a child node.
    /// </summary>
    public virtual bool AcceptsChild(Entity actualEntity, Entity remoteEntity)
    {
        return true;
    }

    /// <summary>
    ///     Checks if <see cref="actualEntity"/> can have the <see cref="remoteEntity"/> as a parent node.
    /// </summary>
    /// <param name="actualEntity"></param>
    /// <param name="remoteEntity"></param>
    /// <param name="remoteTerminal">The target terminal, owned by <see cref="remoteEntity"/></param>
    /// <returns></returns>
    public virtual bool AcceptsParent(Entity actualEntity, Entity remoteEntity, NodeTerminal remoteTerminal)
    {
        return true;
    }

    /// <summary>
    ///     Called to serialize the custom data of this node.
    /// </summary>
    public virtual string Save(Entity entity)
    {
        return "";
    }

    /// <summary>
    ///     Called when the node is being deserialized, before the child nodes are deserialized.
    /// </summary>
    public virtual void InitialLoad(Entity entity, string storedData)
    {

    }

    /// <summary>
    ///     Called when the node is being deserialized, after all child nodes have been loaded.
    /// </summary>
    public virtual void AfterLoad(Entity entity) { }

    public virtual void Analyze(Entity entity, NodeAnalysis analysis) { }

    /// <summary>
    ///     Submits an <see cref="ImGui"/> inspector interface. The call happens inside a window block, so windows should not be created here.
    /// </summary>
    /// <param name="entity">The entity being edited.</param>
    /// <param name="project">The current project.</param>
    /// <returns>True, if the project was mutated.</returns>
    public virtual bool SubmitInspector(Entity entity, Project project)
    {
        return false;
    }

    /// <summary>
    ///     If true, <see cref="OnProjectUpdate"/> will be called when the project version changes.
    /// </summary>
    public virtual bool ListenForProjectUpdate => false;

    /// <summary>
    ///     Called when <see cref="ListenForProjectUpdate"/> is true and the project version has changed.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="project"></param>
    public virtual void OnProjectUpdate(Entity entity, Project project)
    {

    }

    public override string ToString()
    {
        return Name;
    }
}

/// <summary>
///     <see cref="NodeBehavior"/> without any child terminals.
/// </summary>
public class LeafNode : NodeBehavior
{
    public LeafNode(TextureSampler icon, string name) : base(icon, new(0.1f, 0.6f, 0.0f, 0.7f), name) { }
}

/// <summary>
///     <see cref="NodeBehavior"/> with one child terminal that accepts any number of child nodes.
/// </summary>
public class ProxyNode : NodeBehavior
{
    public ProxyNode(TextureSampler icon, string name) : base(icon, new(0.3f, 0.6f, 0.1f, 0.7f), name) { }

    protected override void AttachTerminals(NodeConnectionSet connections)
    {
        connections.AddChildTerminal(new NodeTerminal(NodeTerminalType.Children, 0));
    }
}

/// <summary>
///     <see cref="NodeTerminal"/> that accepts a single child node. When a connection is made, any old connections are unlinked implicitly.
/// </summary>
public class DecoratorTerminal : NodeTerminal
{
    public DecoratorTerminal(int id) : base(NodeTerminalType.Children, id) { }

    public override void PrepareConnection(NodeTerminal other, Entity actualEntity, Entity remoteEntity)
    {
        if (actualEntity.Get<NodeComponent>().ChildrenRef.Instance.Count > 0)
        {
            actualEntity.UnlinkChildren();
        }
    }
}

/// <summary>
///     <see cref="NodeBehavior"/> that accepts only one child using <see cref="DecoratorTerminal"/>
/// </summary>
public class DecoratorNode : NodeBehavior
{
    public DecoratorNode(TextureSampler icon, string name) : base(icon, new(0.25f, 0.25f, 0.75f, 0.5f), name) { }

    protected override void AttachTerminals(NodeConnectionSet connections)
    {
        connections.AddChildTerminal(new DecoratorTerminal(0));
    }

    public override void Analyze(Entity entity, NodeAnalysis analysis)
    {
        analysis.Warn("Warning 1").Warn("Warning 2").Warn("Warning 3").Error("Error 1");
    }
}

public sealed class MotionNode : NodeBehavior
{
    public struct MotionNodeTerminalBinding
    {
        public MotionNodeTerminalBinding(int terminalId, string motionProject, string marker)
        {
            TerminalId = terminalId;
            MotionProject = motionProject;
            Marker = marker;
        }

        [JsonInclude]
        public int TerminalId { get; set; }
        
        [JsonInclude]
        public string MotionProject { get; set; }

        [JsonInclude]
        public string Marker { get; set; }
    }

    public class MotionNodeState
    {
        [JsonInclude]
        public List<MotionNodeTerminalBinding> Bindings { get; set; } = new();

        // GUI state:

        [JsonIgnore]
        public string SelectedProjectLabel = string.Empty;

        [JsonIgnore]
        public string SelectedMarkerLabel = string.Empty;
    }

    public struct MotionNodeComponent
    {
        public MotionNodeState State;
    }

    public MotionNode(TextureSampler icon, string name) : base(icon, new Vector4(0.95f, 0.8f, 0.5f, 0.7f), name)
    {
        
    }

    public override bool ListenForProjectUpdate => true;

    public override void AttachComponents(Entity entity)
    {
        entity.Add(new MotionNodeComponent
        {
            State = new MotionNodeState()
        });
    }

    public override string Save(Entity entity)
    {
        return JsonSerializer.Serialize(entity.Get<MotionNodeComponent>().State);
    }

    public override void InitialLoad(Entity entity, string storedData)
    {
        var state = JsonSerializer.Deserialize<MotionNodeState>(storedData) ?? throw new Exception("Failed to deserialize motion node");

        entity.Get<MotionNodeComponent>().State = state;

        ref var component = ref entity.Get<NodeComponent>();

        foreach (var binding in state.Bindings)
        {
            component.Terminals.AddChildTerminal(new NodeTerminal(NodeTerminalType.Children, binding.TerminalId));
        }
    }

    private static void DestroyBinding(Entity entity, MotionNodeTerminalBinding binding)
    {
        var node = entity.Get<NodeComponent>();
        ref var state = ref entity.Get<MotionNodeComponent>().State;

        node
            .ChildrenRef
            .Instance
            .Where(c => c.Terminal.Id == binding.TerminalId)
            .ToArray()
            .ForEach(child => entity.UnlinkFrom(child.Entity));

        var terminalResults = node.Terminals.ChildTerminals.Where(x => x.Id == binding.TerminalId).ToArray();

        Assert.IsTrue(terminalResults.Length == 1, $"Failed to get the expected number of terminals ({terminalResults.Length})");

        Assert.IsTrue(state.Bindings.Remove(binding));
        node.Terminals.RemoveChildTerminal(terminalResults.First());
    }

    public override bool SubmitInspector(Entity entity, Project project)
    {
        ref var node = ref entity.Get<NodeComponent>();
        ref var state = ref entity.Get<MotionNodeComponent>().State;

        var deleted = new List<MotionNodeTerminalBinding>();

        var changed = false;

        ImGui.PushID("Motion Node Inspector");

        try
        {
            if (ImGui.CollapsingHeader("Marker Bindings"))
            {
                ImGui.BeginGroup();
                {
                    nint bindingIdx = 0;
                    foreach (var binding in state.Bindings)
                    {
                        ImGui.PushID(bindingIdx++);

                        try
                        {
                            ImGui.Text($"Project: {binding.MotionProject}");
                            ImGui.Text($"Marker: {binding.Marker}");

                            ImGui.SameLine();

                            void MarkDelete()
                            {
                                if (!deleted!.Contains(binding))
                                {
                                    deleted!.Add(binding);
                                }

                                changed = true;
                            }

                            if (ImGui.Button("-"))
                            {
                                MarkDelete();
                            }

                            ImGui.Separator();

                            // Destroy bindings with invalid markers:

                            if (!project.MotionProjects.TryGetValue(binding.MotionProject, out var motionProject))
                            {
                                MarkDelete();
                                continue;
                            }

                            if (!motionProject.Markers.Any(x => x.Name.Equals(binding.Marker)))
                            {
                                MarkDelete();
                                continue;
                            }
                        }
                        finally
                        {
                            ImGui.PopID();
                        }
                    }

                    if (state.Bindings.Count == 0)
                    {
                        ImGui.Text("None");
                    }
                }
                
                ImGui.EndGroup();

                ImGui.Separator();

                ImGui.Text("Create New");
                {
                    void LabelScan(string[] names, ref string selected, string comboLbl)
                    {
                        var idx = names.Length == 0 ? -1 : Array.IndexOf(names, selected);

                        if (idx == -1)
                        {
                            idx = 0;
                        }

                        ImGui.Combo(comboLbl, ref idx, names, names.Length);

                        if (names.Length != 0)
                        {
                            idx = Math.Clamp(idx, 0, names.Length);
                            selected = names[idx];
                        }
                    }

                    LabelScan(project.MotionProjects.Keys.ToArray(), ref state.SelectedProjectLabel, "Project");

                    if (project.MotionProjects.TryGetValue(state.SelectedProjectLabel, out var motionProject))
                    {
                        LabelScan(motionProject.Markers.Select(x=>x.Name).ToArray(), ref state.SelectedMarkerLabel, "Marker");

                        var marker = state.SelectedMarkerLabel;

                        if (motionProject.Markers.Any(x => x.Name.Equals(marker)))
                        {
                            if (ImGui.Button("Create"))
                            {
                                var terminalId = node.Terminals.ChildTerminals.Map(terminals =>
                                {
                                    var idx = 0;
                                    var lookup = terminals.Select(x => x.Id).ToHashSet();

                                    while (lookup.Contains(idx))
                                    {
                                        idx++;
                                    }

                                    return idx;
                                });

                                node.Terminals.AddChildTerminal(new NodeTerminal(NodeTerminalType.Children, terminalId));
                                state.Bindings.Add(new MotionNodeTerminalBinding(terminalId, state.SelectedProjectLabel, marker));

                                changed = true;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            ImGui.PopID();
        }

        foreach (var binding in deleted)
        {
            DestroyBinding(entity, binding);
        }

        return changed;
    }

    public override void OnProjectUpdate(Entity entity, Project project)
    {
        ref var state = ref entity.Get<MotionNodeComponent>().State;

        var removed = new List<MotionNodeTerminalBinding>();

        foreach (var binding in state.Bindings)
        {
            if (!project.MotionProjects.TryGetValue(binding.MotionProject, out var motionProject) ||
                !motionProject.Markers.Any(x => x.Name.Equals(binding.Marker)))
            {
                removed.Add(binding);
            }
        }

        foreach (var binding in removed)
        {
            DestroyBinding(entity, binding);
        }
    }
}

public static class NodeExtensions
{
    /// <summary>
    ///     Un-links all the children of this node using <see cref="UnlinkFrom"/>
    /// </summary>
    /// <param name="parent"></param>
    public static void UnlinkChildren(this Entity parent)
    {
        var children = parent.Get<NodeComponent>().ChildrenRef.Instance.ToArray();

        foreach (var child in children)
        {
            parent.UnlinkFrom(child.Entity);
        }

        parent.Get<NodeComponent>().ChildrenRef.Instance.Clear();
    }

    /// <summary>
    ///     Unlink two nodes, removing the child from the parent and the parent from the child component.
    /// </summary>
    /// <param name="parent">The parent node.</param>
    /// <param name="child">The child node.</param>
    public static void UnlinkFrom(this Entity parent, Entity child)
    {
        ref var parentComp = ref parent.Get<NodeComponent>();
        ref var childComp = ref child.Get<NodeComponent>();

        Assert.IsTrue(parentComp.ChildrenRef.Instance.RemoveAll(x => x.Entity == child) == 1, "Unlink invalid child");
        childComp.Parent = null;
    }

    /// <summary>
    ///     Checks if two nodes can link.
    /// </summary>
    /// <param name="parentEntity">The desired parent of <seealso cref="childEntity"/>.</param>
    /// <param name="childEntity">The desired child of <see cref="parentEntity"/>. If this entity already has a parent, it will be removed using <see cref="UnlinkFrom"/>.</param>
    /// <param name="parentTerminal">A terminal of the <see cref="parentEntity"/>.</param>
    /// <returns>True if the two nodes can link, as per <see cref="NodeTerminal.AcceptsConnection"/>, <see cref="NodeBehavior.AcceptsParent"/> and <see cref="NodeBehavior.AcceptsChild"/>. Otherwise, false.</returns>
    public static bool CanLinkTo(this Entity parentEntity, Entity childEntity, NodeTerminal parentTerminal)
    {
        var parent = parentEntity.Get<NodeComponent>();
        var child = childEntity.Get<NodeComponent>();

        return child.Terminals.ParentTerminal.AcceptsConnection(parentTerminal, childEntity, parentEntity) &&
               parentTerminal.AcceptsConnection(child.Terminals.ParentTerminal, parentEntity, childEntity) &&
               child.Behavior.AcceptsParent(childEntity, parentEntity, parentTerminal) &&
               parent.Behavior.AcceptsChild(parentEntity, childEntity);
    }

    /// <summary>
    ///     Links two nodes, removing the old parent from the child node.
    /// </summary>
    /// <param name="parentEntity">The desired parent of <seealso cref="childEntity"/>.</param>
    /// <param name="childEntity">The desired child of <see cref="parentEntity"/>. If this entity already has a parent, it will be removed using <see cref="UnlinkFrom"/>.</param>
    /// <param name="parentTerminal">A terminal of the <see cref="parentEntity"/>.</param>
    public static void LinkTo(this Entity parentEntity, Entity childEntity, NodeTerminal parentTerminal)
    {
        if (!parentEntity.CanLinkTo(childEntity, parentTerminal))
        {
            throw new InvalidOperationException("Cannot link nodes");
        }

        ref var parent = ref parentEntity.Get<NodeComponent>();
        ref var child = ref childEntity.Get<NodeComponent>();

        // Remove old parent:
        child.Parent?.UnlinkFrom(childEntity);

        // Prepare connections:
        child.Terminals.ParentTerminal.PrepareConnection(parentTerminal, childEntity, parentEntity);
        parentTerminal.PrepareConnection(child.Terminals.ParentTerminal, parentEntity, childEntity);

        // Form connections:
        child.Parent = parentEntity;
        parent.ChildrenRef.Instance.Add(new NodeChild(childEntity, parentTerminal));

        child.Terminals.ParentTerminal.FinishConnection(parentTerminal, childEntity, parentEntity);
        parentTerminal.FinishConnection(child.Terminals.ParentTerminal, parentEntity, childEntity);
    }
}