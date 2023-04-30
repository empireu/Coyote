using System;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework.Renderer;
using GameFramework.Scene;
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
    public virtual void AttachComponents(Entity entity) { }

    #region Rules

    public static bool CanLinkChild(Entity actualEntity, Entity childCandidate)
    {
        var subNodes = childCandidate.ToListDown();

        return !actualEntity.TreeAnyUp(super =>
            !super
                .Behavior()
                .AcceptsSubTree(
                    actualEntity: super, 
                    bottomEntityActual: actualEntity, 
                    subTreeCandidate: subNodes, 
                    bottomEntityActualChildCandidate: childCandidate
                 ), 
            true
            );
    }

    public static bool CanLinkParent(Entity actualEntity, Entity parentCandidate)
    {
        var superNodes = parentCandidate.ToListUpDown();
        var siblingsCandidate = parentCandidate.ChildrenEnt().ToHashSet();
        superNodes.RemoveAll(siblingsCandidate.Contains);

        return !actualEntity.TreeAnyDown(sub => 
            !sub
                .Behavior()
                .AcceptsSuperTree(
                    actualEntity: sub, // Actual entity is the sub node being scanned
                    topEntityActual: actualEntity, // Top entity is the entity receiving the parent candidate
                    superTreeCandidate: superNodes, // Super nodes is the entire tree of parentCandidate, inclusive,
                    topEntityActualParentCandidate: parentCandidate
                )
            );
    }

    protected virtual bool AcceptsSubTree(Entity actualEntity, Entity bottomEntityActual, IReadOnlyList<Entity> subTreeCandidate, Entity bottomEntityActualChildCandidate)
    {
        return true;
    }

    protected virtual bool AcceptsSuperTree(Entity actualEntity, Entity topEntityActual, IReadOnlyList<Entity> superTreeCandidate, Entity topEntityActualParentCandidate)
    {
        return true;
    }

    #endregion

    #region Saving

    /// <summary>
    ///     Called to serialize the custom data of this node.
    /// </summary>
    public virtual string Save(Entity entity)
    {
        return string.Empty;
    }

    /// <summary>
    ///     Called when the node is being deserialized, before the child nodes are deserialized.
    /// </summary>
    public virtual void InitialLoad(Entity entity, string storedData) { }

    /// <summary>
    ///     Called when the node is being deserialized, after all child nodes have been loaded.
    /// </summary>
    public virtual void AfterLoad(Entity entity) { }

    #endregion

    public virtual void Analyze(Entity entity, NodeAnalysis analysis, Project project) { }

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
    public virtual void OnProjectUpdate(Entity entity, Project project) { }

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
    public ProxyNode(TextureSampler icon, Vector4 color, string name) : base(icon, color, name) { }
    public ProxyNode(TextureSampler icon, string name) : this(icon, new Vector4(0.3f, 0.6f, 0.1f, 0.7f), name) { }

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
}

#region Behavior Marker Interfaces

// These are mostly used in node connection validation.

/// <summary>
///     Marks a behavior that cannot be executed in parallel.
/// </summary>
public interface INonParallelBehavior { }

/// <summary>
///     Marks a drivetrain behavior. Anything that changes the state of the drivetrain should implement this.
/// </summary>
public interface IDriveBehavior : INonParallelBehavior { }

#endregion

#region Special Implementations

public sealed class MotionNode : NodeBehavior, IDriveBehavior
{
    public struct MotionNodeTerminalBinding
    {
        public MotionNodeTerminalBinding(int terminalId, string marker)
        {
            TerminalId = terminalId;
            Marker = marker;
        }

        [JsonInclude]
        public int TerminalId { get; set; }
      
        [JsonInclude]
        public string Marker { get; set; }
    }

    public class MotionNodeState
    {
        [JsonInclude]
        public string MotionProject { get; set; } = string.Empty;

        [JsonInclude]
        public List<MotionNodeTerminalBinding> Bindings { get; set; } = new();

        // GUI state:
        [JsonIgnore]
        public string SelectedMarkerLabel = string.Empty;
    }

    // Proxy so we don't have to use Ref anywhere.
    public struct MotionNodeComponent
    {
        public MotionNodeState State;
    }

    public MotionNode(TextureSampler icon, string name) : base(icon, new Vector4(0.95f, 0.8f, 0.5f, 0.7f), name) { }
    
    // Listen for marker deletes and updates
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

    /// <summary>
    ///     Destroys the <see cref="binding"/> (removing the terminal from <see cref="NodeComponent.Terminals"/>
    ///     and <see cref="MotionNodeState.Bindings"/>), un-linking all children connected to it using <see cref="NodeExtensions.UnlinkFrom"/>
    /// </summary>
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
            // Show combo box using a string as persistent state and a potentially empty item list.
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

            var projectName = state.MotionProject;
            LabelScan(project.MotionProjects.Keys.ToArray(), ref projectName, "Project");
            state.MotionProject = projectName;

            if (project.MotionProjects.TryGetValue(projectName, out var motionProject))
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
                                ImGui.Text(binding.Marker);

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
                                if (!motionProject.Markers.Any(x => x.Name.Equals(binding.Marker)))
                                {
                                    MarkDelete();
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
                        LabelScan(motionProject.Markers.Select(x => x.Name).ToArray(), ref state.SelectedMarkerLabel, "Marker");

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
                                state.Bindings.Add(new MotionNodeTerminalBinding(terminalId, marker));

                                changed = true;
                            }
                        }
                    }
                }
            }
            else
            {
                // Motion project does not exist, so destroy all bindings.
                state.Bindings.Bind().ForEach(b => DestroyBinding(entity, b));
            }
        }
        finally
        {
            ImGui.PopID();
        }

        deleted.Bind().ForEach(b => DestroyBinding(entity, b));

        return changed;
    }

    public override void OnProjectUpdate(Entity entity, Project project)
    {
        ref var state = ref entity.Get<MotionNodeComponent>().State;

        if (!project.MotionProjects.TryGetValue(state.MotionProject, out var motionProject))
        {
            // Project no longer exists, destroy bindings.

            state
                .Bindings
                .Bind()
                .ForEach(b => DestroyBinding(entity, b));
        }
        else
        {
            // Destroy any bindings whose markers might have gotten deleted.

            state
                .Bindings
                .Where(b => !motionProject.Markers.Any(m => m.Name.Equals(b.Marker)))
                .Bind()
                .ForEach(b => DestroyBinding(entity, b));
        }
    }

    public override void Analyze(Entity entity, NodeAnalysis analysis, Project project)
    {
        var node = entity.Get<NodeComponent>();
        var state = entity.Get<MotionNodeComponent>().State;

        if (!project.MotionProjects.ContainsKey(state.MotionProject))
        {
            analysis.Error("Motion project not set");
        }

        foreach (var binding in state.Bindings.Where(binding => node.ChildrenRef.Instance.All(child => child.Terminal.Id != binding.TerminalId)))
        {
            analysis.Warn($"Empty marker tree for \"{binding.Marker}\"");
        }
    }

    protected override bool AcceptsSuperTree(Entity actualEntity, Entity topEntityActual, IReadOnlyList<Entity> superTreeCandidate, Entity topEntityActualParentCandidate)
    {
        // Find pathway to root from the candidate parent of topEntityActual and ensure there are no motion nodes in it.
        // We assume the pathway from actualEntity to topEntityActual is valid (since it is actual)

        return !topEntityActualParentCandidate
            .TreeEnumerateUp(true)
            .Any(e => e.IsBehavior<MotionNode>());
    }
}

public sealed class ParallelNode : ProxyNode
{
    public ParallelNode(TextureSampler icon, string name) : base(icon, new Vector4(0.3f, 0.5f, 0.1f, 0.8f), name) { }

    /// <summary>
    ///     Prohibits nested parallel nodes.
    /// </summary>
    protected override bool AcceptsSuperTree(Entity actualEntity, Entity topEntityActual, IReadOnlyList<Entity> superTreeCandidate, Entity topEntityActualParentCandidate)
    {
        return topEntityActualParentCandidate
            .TreeEnumerateUp(true)
            .NoneHaveBehavior<ParallelNode>();
    }

    /// <summary>
    ///     Prohibits <see cref="INonParallelBehavior"/> of the same type in parallel branches.
    /// </summary>
    protected override bool AcceptsSubTree(Entity actualEntity, Entity bottomEntityActual, IReadOnlyList<Entity> subTreeCandidate, Entity bottomEntityActualChildCandidate)
    {
        // Handling for INonParallelBehaviors of different types:
        var subTypes = subTreeCandidate
            .WithBehavior<INonParallelBehavior>()
            .Select(x => x.Behavior().GetType())
            .ToHashSet();

        if (subTypes.Count == 0)
        {
            // Fine to merge because there aren't any NPBs in the subtree.
            return true;
        }

        // The parallel node prohibits subtree configurations that would result in a non-parallel-behavior (NPB) of the same type being executed in parallel.
        // Being in parallel means the NPB belongs to two separate subtrees (of the parallel). The subtrees start at the actual children nodes.
        // Multiple NPBs of the same type are allowed as long as they are under the same subtree, because nested parallel nodes are prohibited by the super tree rules.
        // An example would be multiple NPBs of the same type under a proxy (e.g. sequence). The proxy is executed in parallel, but the proxy executes the nodes in some sequential order.
        // NPBs of different types are allowed to run in parallel (e.g. a drive call can run in parallel with some other actuator call).

        return subTypes.All(type => 
            actualEntity
                .ChildrenEnt()
                .Select(child => child  // Generate all actual subtrees
                    .TreeEnumerateDown()
                    .ToHashSet())
                .Where(subTree => subTree // Select subtrees which contain the NPB of type
                    .Any(x => x.Behavior().GetType() == type))
                .All(subTree => subTree.Contains(bottomEntityActual))); // Ensure that the root of the candidate child is in one of the subtrees. If it is not, it is an independent tree and thus illegal.
    }
}

#endregion

public static class NodeExtensions
{
    public static NodeBehavior Behavior(this Entity entity) => entity.Get<NodeComponent>().Behavior;
    public static List<NodeChild> Children(this Entity entity) => entity.Get<NodeComponent>().ChildrenRef.Instance;
    public static IEnumerable<Entity> ChildrenEnt(this Entity entity) => entity.Children().Select(x => x.Entity);

    /// <summary>
    ///     Un-links all the children of this node using <see cref="UnlinkFrom"/> and clears the children collection.
    /// </summary>
    /// <param name="parent"></param>
    public static void UnlinkChildren(this Entity parent)
    {
        parent.Get<NodeComponent>().ChildrenRef.Instance.Bind().ForEach(c => parent.UnlinkFrom(c.Entity));
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
    ///     Checks if two nodes can link using the rules imposed by <see cref="NodeBehavior.CanLinkParent"/>, <see cref="NodeBehavior.CanLinkChild"/> and <see cref="NodeTerminal.AcceptsConnection"/>.
    /// </summary>
    /// <param name="parentEntity">The desired parent of <seealso cref="childEntity"/>.</param>
    /// <param name="childEntity">The desired child of <see cref="parentEntity"/>. If this entity already has a parent, it will be removed using <see cref="UnlinkFrom"/>.</param>
    /// <param name="parentTerminal">A terminal of the <see cref="parentEntity"/>.</param>
    /// <returns>True if the two nodes can link, as per <see cref="NodeTerminal.AcceptsConnection"/>, <see cref="NodeBehavior.CanLinkParent"/> and <see cref="NodeBehavior.CanLinkChild"/>. Otherwise, false.</returns>
    public static bool CanLinkTo(this Entity parentEntity, Entity childEntity, NodeTerminal parentTerminal)
    {
        var parent = parentEntity.Get<NodeComponent>();
        var child = childEntity.Get<NodeComponent>();

        return child.Terminals.ParentTerminal.AcceptsConnection(parentTerminal, childEntity, parentEntity) &&
               parentTerminal.AcceptsConnection(child.Terminals.ParentTerminal, parentEntity, childEntity) &&
               NodeBehavior.CanLinkParent(childEntity, parentEntity) &&
               NodeBehavior.CanLinkChild(parentEntity, childEntity);
    }

    /// <summary>
    ///     Links two nodes, removing the old parent from the child node.
    ///     Firstly, the old parent of <see cref="childEntity"/> is removed using <see cref="UnlinkFrom"/>.
    ///     Then, <see cref="NodeTerminal.PrepareConnection"/> is called on both terminals to prepare the connection.
    ///     Finally, the parent of the <see cref="childEntity"/> is updated, <see cref="childEntity"/> is added to the <see cref="parentEntity"/>'s children list, and <see cref="NodeTerminal.FinishConnection"/> is called on both terminals.
    /// </summary>
    /// <param name="parentEntity">The desired parent of <seealso cref="childEntity"/>.</param>
    /// <param name="childEntity">The desired child of <see cref="parentEntity"/>. If this entity already has a parent, it will be removed using <see cref="UnlinkFrom"/>.</param>
    /// <param name="parentTerminal">A terminal of the <see cref="parentEntity"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown if the <see cref="CanLinkTo"/> test fails.</exception>
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

    /// <summary>
    ///     Tree scan consumer, used in Node Tree traversals.
    /// </summary>
    /// <param name="entity">The entity found during the search.</param>
    /// <returns>If true, the search will continue. Otherwise, the search will end and no more entities will be discovered.</returns>
    public delegate bool TreeScanConsumerDelegate(in Entity entity);

    #region Downwards Scan

    /// <summary>
    ///     Scans the tree in Depth First Order, starting from <see cref="root"/> inclusively.
    /// </summary>
    /// <returns>True if the search finished. False if <see cref="consumer"/> returned false.</returns>
    public static bool TreeEnumerateDown(this Entity root, TreeScanConsumerDelegate consumer)
    {
        consumer(root);

        var children = root.Get<NodeComponent>().ChildrenRef.Instance;

        for (var i = 0; i < children.Count; i++)
        {
            if (!TreeEnumerateDown(children[i].Entity, consumer))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Scans the tree using <see cref="TreeEnumerateDown(Arch.Core.Entity,Coyote.App.Nodes.NodeExtensions.TreeScanConsumerDelegate)"/> and adds the results to <see cref="results"/>.
    /// </summary>
    public static void CollectDown(this Entity root, ICollection<Entity> results)
    {
        root.TreeEnumerateDown((in Entity e) =>
        {
            results.Add(e);
            return true;
        });
    }

    public static List<Entity> ToListDown(this Entity root)
    {
        return new List<Entity>().Also(l => CollectDown(root, l));
    }

    public static int TreeCountDown(this Entity root, Predicate<Entity> predicate)
    {
        var result = 0;

        root.TreeEnumerateDown((in Entity e) =>
        {
            if (predicate(e))
            {
                ++result;
            }

            return true;
        });

        return result;
    }

    public static Entity? TreeFindOrNullDown(this Entity root, Predicate<Entity> predicate)
    {
        Entity? result = null;

        root.TreeEnumerateDown((in Entity e) =>
        {
            if (predicate(e))
            {
                result = e;
                return false;
            }

            return true;
        });

        return result;
    }

    public static bool TreeAnyDown(this Entity root, Predicate<Entity> predicate)
    {
        var found = false;

        root.TreeEnumerateDown((in Entity e) =>
        {
            if (predicate(e))
            {
                found = true;
                return false;
            }

            return true;
        });

        return found;
    }

    #endregion

    #region Upwards Scan

    /// <summary>
    ///     Scans the unique path to the root node, starting from <see cref="lowermost"/>.
    /// </summary>
    public static void TreeEnumerateUp(this Entity lowermost, TreeScanConsumerDelegate consumer, bool inclusive)
    {
        if (!inclusive)
        {
            var next = lowermost.Get<NodeComponent>().Parent;

            if (next == null)
            {
                return;
            }

            lowermost = next.Value;
        }

        while (true)
        {
            if (!consumer(lowermost))
            {
                break;
            }

            ref var component = ref lowermost.Get<NodeComponent>();

            if (component.Parent == null)
            {
                return;
            }

            lowermost = component.Parent.Value;
        }
    }

    public static void CollectUp(this Entity root, ICollection<Entity> results, bool inclusive)
    {
        root.TreeEnumerateUp((in Entity e) =>
        {
            results.Add(e);
            return true;
        }, inclusive);
    }

    public static Entity? TreeFindOrNullUp(this Entity root, Predicate<Entity> predicate, bool inclusive)
    {
        Entity? result = null;

        root.TreeEnumerateUp((in Entity e) =>
        {
            if (predicate(e))
            {
                result = e;
                return false;
            }

            return true;
        }, inclusive);

        return result;
    }

    public static bool TreeAnyUp(this Entity lowermost, Predicate<Entity> predicate, bool inclusive)
    {
        var found = false;

        lowermost.TreeEnumerateUp((in Entity e) =>
        {
            if (predicate(e))
            {
                found = true;
                return false;
            }

            return true;
        }, inclusive);

        return found;
    }
   
    #endregion

    public static Entity FindRoot(this Entity entity)
    {
        while (true)
        {
            var component = entity.Get<NodeComponent>();

            if (component.Parent == null)
            {
                return entity;
            }

            entity = component.Parent.Value;
        }
    }

    public static void TreeScanUpDown(this Entity entity, TreeScanConsumerDelegate consumer)
    {
        entity.FindRoot().TreeEnumerateDown(consumer);
    }

    public static void CollectUpDown(this Entity entity, ICollection<Entity> results)
    {
        entity.TreeScanUpDown((in Entity e) =>
        {
            results.Add(e);
            return true;
        });
    }

    public static List<Entity> ToListUpDown(this Entity entity)
    {
        return new List<Entity>().Also(l => entity.CollectUpDown(l));
    }

    public static bool TreeAnyUpDown(this Entity entity, Predicate<Entity> predicate)
    {
        return entity.FindRoot().TreeAnyDown(predicate);
    }

    public static IEnumerable<Entity> TreeEnumerateUp(this Entity lowermost, bool inclusive)
    {
        if (!inclusive)
        {
            var next = lowermost.Get<NodeComponent>().Parent;

            if (next == null)
            {
                yield break;
            }

            lowermost = next.Value;
        }

        while (true)
        {
            yield return lowermost;

            var component = lowermost.Get<NodeComponent>();

            if (component.Parent == null)
            {
                yield break;
            }

            lowermost = component.Parent.Value;
        }
    }

    public static IEnumerable<Entity> TreeEnumerateDown(this Entity root)
    {
        var stack = new Stack<Entity>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            root = stack.Pop();

            yield return root;

            var children = root.Get<NodeComponent>().ChildrenRef.Instance;

            for (var i = 0; i < children.Count; i++)
            {
                stack.Push(children[i].Entity);
            }
        }
    }

    public static bool IsBehavior<TBehavior>(this Entity entity)
    {
        return entity.Get<NodeComponent>().Behavior is TBehavior;
    }

    public static bool IsNotBehavior<TBehavior>(this Entity entity)
    {
        return entity.Get<NodeComponent>().Behavior is not TBehavior;
    }

    public static bool AnyWithBehavior<TBehavior>(this IEnumerable<Entity> enumerable)
    {
        return enumerable.Any(x => x.IsBehavior<TBehavior>());
    }

    public static IEnumerable<Entity> WithBehavior<TBehavior>(this IEnumerable<Entity> enumerable)
    {
        return enumerable.Where(x => x.IsBehavior<TBehavior>());
    }

    public static bool NoneHaveBehavior<TBehavior>(this IEnumerable<Entity> entity)
    {
        return entity.All(e => e.IsNotBehavior<TBehavior>());
    }
}